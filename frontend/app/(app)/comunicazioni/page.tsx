"use client";

import { useEffect, useState, type FormEvent } from "react";
import { api, scaricaBlob } from "@/lib/api";
import type { ComunicazioneDto, DestinatariComunicazione } from "@/lib/types";
import { Button, Card, EmptyState, ErrorBlock, Field, Input, LoadingBlock, PageHeader, Select } from "@/components/ui";
import { formattaDataOra, formattaDimensioneFile, messaggioErrore } from "@/lib/format";
import { isSoloDirezione, useAuth } from "@/lib/auth-context";

const ICONE_PER_ESTENSIONE: Record<string, string> = {
  pdf: "📄",
  xlsx: "📊",
  xls: "📊",
  doc: "📝",
  docx: "📝",
};

const ETICHETTE_DESTINATARI: Record<DestinatariComunicazione, string> = {
  Tutti: "Agenti e clienti",
  SoloAgenti: "Solo agenti",
  SoloClienti: "Solo clienti",
};

function estensione(nomeFile: string): string {
  return nomeFile.split(".").pop()?.toLowerCase() ?? "";
}

export default function ComunicazioniPage() {
  const { utente } = useAuth();
  const puoPubblicare = isSoloDirezione(utente?.ruolo);

  const [elenco, setElenco] = useState<ComunicazioneDto[] | null>(null);
  const [caricando, setCaricando] = useState(true);
  const [errore, setErrore] = useState<string | null>(null);

  const [titolo, setTitolo] = useState("");
  const [descrizione, setDescrizione] = useState("");
  const [destinatari, setDestinatari] = useState<DestinatariComunicazione>("Tutti");
  const [file, setFile] = useState<File | null>(null);
  const [inviando, setInviando] = useState(false);

  const [scaricandoId, setScaricandoId] = useState<number | null>(null);
  const [eliminandoId, setEliminandoId] = useState<number | null>(null);

  function carica() {
    setCaricando(true);
    api.comunicazioni.lista()
      .then(setElenco)
      .catch((err) => setErrore(messaggioErrore(err)))
      .finally(() => setCaricando(false));
  }

  useEffect(carica, []);

  async function handlePubblica(e: FormEvent) {
    e.preventDefault();
    if (!file) {
      setErrore("Seleziona un file da pubblicare.");
      return;
    }
    if (!titolo.trim()) {
      setErrore("Il titolo è obbligatorio.");
      return;
    }
    setErrore(null);
    setInviando(true);
    try {
      await api.comunicazioni.crea(file, titolo.trim(), descrizione.trim(), destinatari);
      setTitolo("");
      setDescrizione("");
      setDestinatari("Tutti");
      setFile(null);
      carica();
    } catch (err) {
      setErrore(messaggioErrore(err));
    } finally {
      setInviando(false);
    }
  }

  async function handleScarica(c: ComunicazioneDto) {
    setScaricandoId(c.id);
    setErrore(null);
    try {
      const blob = await api.comunicazioni.scarica(c.id);
      scaricaBlob(blob, c.nomeFile);
    } catch (err) {
      setErrore(messaggioErrore(err));
    } finally {
      setScaricandoId(null);
    }
  }

  async function handleCambiaDestinatari(c: ComunicazioneDto, nuovi: DestinatariComunicazione) {
    setErrore(null);
    try {
      await api.comunicazioni.aggiornaDestinatari(c.id, nuovi);
      setElenco((prev) => prev?.map((x) => (x.id === c.id ? { ...x, destinatari: nuovi } : x)) ?? prev);
    } catch (err) {
      setErrore(messaggioErrore(err));
    }
  }

  async function handleElimina(c: ComunicazioneDto) {
    const conferma = window.confirm(`Eliminare la comunicazione "${c.titolo}"? L'operazione non è reversibile dall'interfaccia.`);
    if (!conferma) return;

    setEliminandoId(c.id);
    setErrore(null);
    try {
      await api.comunicazioni.elimina(c.id);
      carica();
    } catch (err) {
      setErrore(messaggioErrore(err));
    } finally {
      setEliminandoId(null);
    }
  }

  return (
    <div className="max-w-3xl">
      <PageHeader title="Comunicazioni" subtitle="Circolari, PDF e file Excel condivisi con la rete vendita e i clienti" />

      {errore && <div className="mb-4"><ErrorBlock message={errore} /></div>}

      {puoPubblicare && (
        <Card className="mb-6">
          <p className="mb-3 text-sm font-medium text-zinc-700">Pubblica una nuova comunicazione</p>
          <form onSubmit={handlePubblica} className="space-y-3">
            <Field label="Titolo *">
              <Input required value={titolo} onChange={(e) => setTitolo(e.target.value)} />
            </Field>
            <Field label="Descrizione">
              <textarea
                className="w-full rounded-md border border-zinc-300 px-3 py-2 text-sm focus:border-teal-600 focus:outline-none focus:ring-1 focus:ring-teal-600"
                rows={2}
                value={descrizione}
                onChange={(e) => setDescrizione(e.target.value)}
              />
            </Field>
            <Field label="Destinatari *">
              <Select value={destinatari} onChange={(e) => setDestinatari(e.target.value as DestinatariComunicazione)} className="w-56">
                {(Object.keys(ETICHETTE_DESTINATARI) as DestinatariComunicazione[]).map((d) => (
                  <option key={d} value={d}>{ETICHETTE_DESTINATARI[d]}</option>
                ))}
              </Select>
            </Field>
            <Field label="File (PDF, Excel o Word — max 20 MB) *">
              <input
                type="file" accept=".pdf,.xlsx,.xls,.doc,.docx"
                onChange={(e) => setFile(e.target.files?.[0] ?? null)}
                className="text-sm"
              />
            </Field>
            <Button type="submit" disabled={inviando}>{inviando ? "Pubblicazione in corso…" : "Pubblica"}</Button>
          </form>
        </Card>
      )}

      {caricando && !elenco ? (
        <LoadingBlock />
      ) : !elenco || elenco.length === 0 ? (
        <EmptyState message="Nessuna comunicazione pubblicata." />
      ) : (
        <div className="space-y-3">
          {elenco.map((c) => (
            <Card key={c.id}>
              <div className="flex items-start justify-between gap-4">
                <div className="flex items-start gap-3">
                  <span className="text-2xl">{ICONE_PER_ESTENSIONE[estensione(c.nomeFile)] ?? "📎"}</span>
                  <div>
                    <p className="font-medium text-zinc-900">{c.titolo}</p>
                    {c.descrizione && <p className="mt-0.5 text-sm text-zinc-600">{c.descrizione}</p>}
                    {puoPubblicare && (
                      <div className="mt-2 flex items-center gap-2 text-xs text-zinc-500">
                        Destinatari:
                        <Select
                          value={c.destinatari}
                          onChange={(e) => handleCambiaDestinatari(c, e.target.value as DestinatariComunicazione)}
                          className="w-44 !py-1 text-xs"
                        >
                          {(Object.keys(ETICHETTE_DESTINATARI) as DestinatariComunicazione[]).map((d) => (
                            <option key={d} value={d}>{ETICHETTE_DESTINATARI[d]}</option>
                          ))}
                        </Select>
                      </div>
                    )}
                    <p className="mt-1 text-xs text-zinc-400">
                      {c.nomeFile} · {formattaDimensioneFile(c.dimensioneByte)} · pubblicato da {c.utentePubblicazioneNomeCompleto} il {formattaDataOra(c.dataPubblicazione)}
                    </p>
                  </div>
                </div>
                <div className="flex shrink-0 gap-2">
                  <Button variant="secondary" onClick={() => handleScarica(c)} disabled={scaricandoId === c.id}>
                    {scaricandoId === c.id ? "…" : "Scarica"}
                  </Button>
                  {puoPubblicare && (
                    <Button variant="ghost" onClick={() => handleElimina(c)} disabled={eliminandoId === c.id}>
                      {eliminandoId === c.id ? "…" : "Elimina"}
                    </Button>
                  )}
                </div>
              </div>
            </Card>
          ))}
        </div>
      )}
    </div>
  );
}
