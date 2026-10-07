"use client";

import { useEffect, useState } from "react";
import { api } from "@/lib/api";
import type { AgenteDto, ClienteDto, DashboardKpiDto, FornitoreDto, ProvvigioneClienteDto, PuntoGraficoMensileDto } from "@/lib/types";
import { Badge, Card, EmptyState, ErrorBlock, LoadingBlock, PageHeader, Select, Table, Td, Th } from "@/components/ui";
import { KpiCard } from "@/components/KpiCard";
import { BarChart, LegendaAnni } from "@/components/BarChart";
import { MultiSelectMesi } from "@/components/MultiSelectMesi";
import { MultiSelectFornitori } from "@/components/MultiSelectFornitori";
import { formattaPercentuale, formattaValuta, messaggioErrore, NOMI_MESI } from "@/lib/format";
import { useAuth, puoVedereQuotaAdm, isCliente } from "@/lib/auth-context";

const ORA = new Date();
const ANNI = Array.from({ length: 5 }, (_, i) => ORA.getFullYear() - i);

export default function DashboardPage() {
  const { utente } = useAuth();
  const vedeQuotaAdm = puoVedereQuotaAdm(utente?.ruolo);
  // Un account Cliente vede solo il proprio fatturato: niente filtri agente/cliente, provvigioni o analisi per agente.
  const soloProprioFatturato = isCliente(utente?.ruolo);
  const [mesi, setMesi] = useState<number[]>([ORA.getMonth() + 1]);
  const [anno, setAnno] = useState(ORA.getFullYear());
  const [agenteId, setAgenteId] = useState<number | "">("");
  const [clienteId, setClienteId] = useState<number | "">("");
  const [fornitoreIds, setFornitoreIds] = useState<number[]>([]);

  const [agenti, setAgenti] = useState<AgenteDto[]>([]);
  const [clienti, setClienti] = useState<ClienteDto[]>([]);
  const [fornitori, setFornitori] = useState<FornitoreDto[]>([]);

  const [kpi, setKpi] = useState<DashboardKpiDto | null>(null);
  const [serie, setSerie] = useState<PuntoGraficoMensileDto[]>([]);
  const [provvigioni, setProvvigioni] = useState<ProvvigioneClienteDto[]>([]);

  const [caricando, setCaricando] = useState(true);
  const [errore, setErrore] = useState<string | null>(null);
  const [ordinamentoFatturato, setOrdinamentoFatturato] = useState<"asc" | "desc" | null>(null);

  // Elenco agenti e fornitori per i filtri: caricati una sola volta.
  useEffect(() => {
    if (!soloProprioFatturato) api.agenti.lista().then(setAgenti).catch((err) => setErrore(messaggioErrore(err)));
    api.fornitori.lista().then(setFornitori).catch((err) => setErrore(messaggioErrore(err)));
  }, [soloProprioFatturato]);

  // Elenco clienti per il filtro "Cliente": ristretto all'agente selezionato, se presente.
  useEffect(() => {
    if (soloProprioFatturato) return;
    api.clienti.lista({ dimensione: 500, agenteId: agenteId || undefined })
      .then((r) => setClienti(r.elementi))
      .catch((err) => setErrore(messaggioErrore(err)));
  }, [agenteId, soloProprioFatturato]);

  // Se cambio l'agente, la scelta di cliente precedente potrebbe non appartenergli più.
  function handleCambiaAgente(valore: string) {
    setAgenteId(valore ? Number(valore) : "");
    setClienteId("");
  }

  useEffect(() => {
    let annullato = false;
    setCaricando(true);
    setErrore(null);

    if (mesi.length === 0) {
      setKpi(null);
      setProvvigioni([]);
      setCaricando(false);
      return;
    }

    const fornitoreIdsAttivi = fornitoreIds.length > 0 ? fornitoreIds : undefined;
    Promise.all([
      api.dashboard.kpi(mesi, anno, agenteId || undefined, clienteId || undefined, fornitoreIdsAttivi),
      api.dashboard.fatturatoMensile(anno, agenteId || undefined, clienteId || undefined, fornitoreIdsAttivi),
      soloProprioFatturato
        ? Promise.resolve([] as ProvvigioneClienteDto[])
        : api.dashboard.provvigioni(mesi, anno, agenteId || undefined, clienteId || undefined, fornitoreIdsAttivi),
    ])
      .then(([kpiRisposta, serieRisposta, provvigioniRisposta]) => {
        if (annullato) return;
        setKpi(kpiRisposta);
        setSerie(serieRisposta);
        setProvvigioni(provvigioniRisposta);
      })
      .catch((err) => !annullato && setErrore(messaggioErrore(err)))
      .finally(() => !annullato && setCaricando(false));

    return () => {
      annullato = true;
    };
  }, [mesi, anno, agenteId, clienteId, fornitoreIds, soloProprioFatturato]);

  // Confronto anno su anno nel grafico: anno selezionato + i due precedenti (quando ci sono dati).
  const anniConfronto = [anno, anno - 1, anno - 2];
  const totaliPerAnno = new Map<number, number>(
    anniConfronto.map((a) => [a, serie.filter((s) => s.anno === a).reduce((somma, s) => somma + s.valore, 0)])
  );
  const crescitaAnnoSuAnno = anniConfronto.slice(0, -1).map((a, i) => {
    const annoPrecedente = anniConfronto[i + 1];
    const corrente = totaliPerAnno.get(a) ?? 0;
    const precedente = totaliPerAnno.get(annoPrecedente) ?? 0;
    const percentuale = precedente === 0 ? (corrente === 0 ? 0 : 100) : Math.round(((corrente - precedente) / precedente) * 1000) / 10;
    return { anno: a, annoPrecedente, percentuale, positiva: percentuale >= 0 };
  });

  const totaleProvvigioni = provvigioni.reduce((somma, p) => somma + p.importoProvvigione, 0);
  const totaleProvvigioneAdm = provvigioni.reduce((somma, p) => somma + p.importoProvvigioneAdm, 0);
  const totaleDifferenza = totaleProvvigioneAdm - totaleProvvigioni;

  const provvigioniOrdinate =
    ordinamentoFatturato === null
      ? provvigioni
      : [...provvigioni].sort((a, b) =>
          ordinamentoFatturato === "asc" ? a.fatturato - b.fatturato : b.fatturato - a.fatturato
        );

  function handleOrdinaFatturato() {
    setOrdinamentoFatturato((attuale) =>
      attuale === null ? "desc" : attuale === "desc" ? "asc" : null
    );
  }

  // Raggruppa il portafoglio (già filtrato per agente/cliente/periodo) per agente,
  // per evidenziare i 5 clienti migliori e i clienti a fatturato zero di ciascuno.
  const analisiPerAgente = Array.from(
    provvigioni
      .reduce((mappa, p) => {
        if (!mappa.has(p.agenteId)) {
          mappa.set(p.agenteId, { agenteId: p.agenteId, agenteNomeCompleto: p.agenteNomeCompleto, clienti: [] as ProvvigioneClienteDto[] });
        }
        mappa.get(p.agenteId)!.clienti.push(p);
        return mappa;
      }, new Map<number, { agenteId: number; agenteNomeCompleto: string; clienti: ProvvigioneClienteDto[] }>())
      .values()
  )
    .map((gruppo) => ({
      ...gruppo,
      migliori: [...gruppo.clienti].filter((c) => c.fatturato > 0).sort((a, b) => b.fatturato - a.fatturato).slice(0, 5),
      senzaFatturato: gruppo.clienti.filter((c) => c.fatturato === 0).slice(0, 5),
    }))
    .sort((a, b) => a.agenteNomeCompleto.localeCompare(b.agenteNomeCompleto));

  const etichettaPeriodo =
    mesi.length === 0
      ? "nessun mese selezionato"
      : mesi.length === 12
        ? `Anno ${anno}`
        : [...mesi].sort((a, b) => a - b).map((m) => NOMI_MESI[m - 1]).join(", ") + ` ${anno}`;

  return (
    <div>
      <PageHeader
        title="Dashboard"
        subtitle="Andamento commerciale della rete vendita"
        actions={
          <>
            {!soloProprioFatturato && (
              <>
                <Select value={agenteId} onChange={(e) => handleCambiaAgente(e.target.value)} className="w-44">
                  <option value="">Tutti gli agenti</option>
                  {agenti.map((a) => <option key={a.id} value={a.id}>{a.nome} {a.cognome}</option>)}
                </Select>
                <Select value={clienteId} onChange={(e) => setClienteId(e.target.value ? Number(e.target.value) : "")} className="w-48">
                  <option value="">Tutto il portafoglio</option>
                  {clienti.map((c) => <option key={c.id} value={c.id}>{c.ragioneSociale}</option>)}
                </Select>
              </>
            )}
            <MultiSelectFornitori fornitori={fornitori} selezionati={fornitoreIds} onChange={setFornitoreIds} className="w-44" />
            <MultiSelectMesi mesiSelezionati={mesi} onChange={setMesi} className="w-40" />
            <Select value={anno} onChange={(e) => setAnno(Number(e.target.value))} className="w-24">
              {ANNI.map((a) => (
                <option key={a} value={a}>{a}</option>
              ))}
            </Select>
          </>
        }
      />

      {errore && <ErrorBlock message={errore} />}
      {mesi.length === 0 ? (
        <EmptyState message="Seleziona almeno un mese per visualizzare i dati." />
      ) : caricando && !kpi ? (
        <LoadingBlock />
      ) : kpi ? (
        <>
          <div className={`grid grid-cols-1 gap-4 sm:grid-cols-2 ${soloProprioFatturato ? "lg:grid-cols-3" : "lg:grid-cols-4"}`}>
            <KpiCard titolo="Fatturato mensile" kpi={kpi.fatturatoMensile} formatta={formattaValuta} />
            {!soloProprioFatturato && <KpiCard titolo="Nuovi clienti" kpi={kpi.nuoviClienti} formatta={(v) => v.toFixed(0)} />}
            <KpiCard titolo="Ordine medio" kpi={kpi.ordineMedio} formatta={formattaValuta} />
            <KpiCard titolo="Cucine vendute" kpi={kpi.cucineVendute} formatta={(v) => v.toFixed(0)} />
          </div>

          <Card className="mt-6">
            <div className="mb-4 flex flex-wrap items-center justify-between gap-3">
              <p className="text-sm font-medium text-zinc-700">
                Fatturato mensile — confronto {anniConfronto.join(", ")}
              </p>
              <div className="flex flex-wrap items-center gap-3">
                {crescitaAnnoSuAnno.map((c) => (
                  <span key={c.anno} className="flex items-center gap-1.5 text-xs text-zinc-500">
                    {c.anno} vs {c.annoPrecedente}
                    <Badge tone={c.positiva ? "green" : "red"}>
                      {c.positiva ? "▲" : "▼"} {formattaPercentuale(Math.abs(c.percentuale))}
                    </Badge>
                  </span>
                ))}
              </div>
            </div>
            <BarChart dati={serie} anni={anniConfronto} />
            <div className="mt-3">
              <LegendaAnni anni={anniConfronto} />
            </div>
          </Card>

          {!soloProprioFatturato && (
          <>
          <Card className="mt-6">
            <div className="mb-4 flex flex-wrap items-center justify-between gap-2">
              <p className="text-sm font-medium text-zinc-700">
                Fatturato e provvigioni per cliente — {etichettaPeriodo}
              </p>
              <div className="flex flex-wrap gap-x-5 gap-y-1 text-sm text-zinc-500">
                {vedeQuotaAdm && (
                  <>
                    <span>
                      Totale ADM (12%): <span className="font-medium text-zinc-800">{formattaValuta(totaleProvvigioneAdm)}</span>
                    </span>
                    <span>
                      Differenza: <span className="font-medium text-zinc-800">{formattaValuta(totaleDifferenza)}</span>
                    </span>
                  </>
                )}
                <span>
                  Totale provvigioni: <span className="font-medium text-zinc-800">{formattaValuta(totaleProvvigioni)}</span>
                </span>
              </div>
            </div>

            {mesi.length === 0 ? (
              <EmptyState message="Seleziona almeno un mese." />
            ) : provvigioni.length === 0 ? (
              <EmptyState message="Nessun cliente trovato per questi filtri." />
            ) : (
              <Table>
                <thead>
                  <tr>
                    <Th>Cliente</Th>
                    <Th>Agente</Th>
                    <Th onClick={handleOrdinaFatturato}>
                      Fatturato {ordinamentoFatturato === "desc" ? "▼" : ordinamentoFatturato === "asc" ? "▲" : ""}
                    </Th>
                    <Th>% Provvigione</Th>
                    <Th>Provvigione (€)</Th>
                    {vedeQuotaAdm && (
                      <>
                        <Th>% ADM</Th>
                        <Th>ADM (€)</Th>
                        <Th>Differenza ADM–agente (€)</Th>
                      </>
                    )}
                  </tr>
                </thead>
                <tbody>
                  {provvigioniOrdinate.map((p) => (
                    <tr key={p.clienteId}>
                      <Td className="font-medium text-zinc-900">{p.ragioneSociale}</Td>
                      <Td>{p.agenteNomeCompleto}</Td>
                      <Td>{formattaValuta(p.fatturato)}</Td>
                      <Td>{formattaPercentuale(p.percentualeProvvigione)}</Td>
                      <Td>{formattaValuta(p.importoProvvigione)}</Td>
                      {vedeQuotaAdm && (
                        <>
                          <Td>{formattaPercentuale(p.percentualeProvvigioneAdm)}</Td>
                          <Td>{formattaValuta(p.importoProvvigioneAdm)}</Td>
                          <Td>{formattaValuta(p.differenzaAdmAgente)}</Td>
                        </>
                      )}
                    </tr>
                  ))}
                </tbody>
              </Table>
            )}
          </Card>

          <Card className="mt-6">
            <p className="mb-4 text-sm font-medium text-zinc-700">
              Migliori e peggiori clienti per agente — {etichettaPeriodo}
            </p>

            {analisiPerAgente.length === 0 ? (
              <EmptyState message="Nessun cliente trovato per questi filtri." />
            ) : (
              <div className="grid grid-cols-1 gap-4 lg:grid-cols-2">
                {analisiPerAgente.map((a) => (
                  <div key={a.agenteId} className="rounded-lg border border-zinc-200 p-4">
                    <p className="mb-3 text-sm font-semibold text-zinc-800">{a.agenteNomeCompleto}</p>
                    <div className="grid grid-cols-1 gap-4 sm:grid-cols-2">
                      <div>
                        <p className="mb-2 text-xs font-medium uppercase tracking-wide text-emerald-600">
                          Top 5 clienti
                        </p>
                        {a.migliori.length === 0 ? (
                          <p className="text-xs text-zinc-400">Nessun fatturato nel periodo.</p>
                        ) : (
                          <ol className="space-y-1.5 text-sm">
                            {a.migliori.map((c, i) => (
                              <li key={c.clienteId} className="flex items-baseline justify-between gap-2">
                                <span className="truncate text-zinc-700">{i + 1}. {c.ragioneSociale}</span>
                                <span className="shrink-0 font-medium text-zinc-900">{formattaValuta(c.fatturato)}</span>
                              </li>
                            ))}
                          </ol>
                        )}
                      </div>
                      <div>
                        <p className="mb-2 text-xs font-medium uppercase tracking-wide text-rose-600">
                          Clienti senza fatturato
                        </p>
                        {a.senzaFatturato.length === 0 ? (
                          <p className="text-xs text-zinc-400">Tutti i clienti hanno fatturato nel periodo.</p>
                        ) : (
                          <ul className="space-y-1.5 text-sm">
                            {a.senzaFatturato.map((c) => (
                              <li key={c.clienteId} className="truncate text-zinc-700">{c.ragioneSociale}</li>
                            ))}
                          </ul>
                        )}
                      </div>
                    </div>
                  </div>
                ))}
              </div>
            )}
          </Card>
          </>
          )}
        </>
      ) : null}
    </div>
  );
}
