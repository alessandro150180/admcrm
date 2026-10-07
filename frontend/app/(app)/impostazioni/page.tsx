"use client";

import { useEffect, useState, type FormEvent } from "react";
import { api } from "@/lib/api";
import { Badge, Button, Card, ErrorBlock, Field, Input, PageHeader } from "@/components/ui";
import { messaggioErrore } from "@/lib/format";
import { isCliente, useAuth } from "@/lib/auth-context";

export default function ImpostazioniPage() {
  const { utente } = useAuth();
  const mostraGoogle = !isCliente(utente?.ruolo);
  const [googleCollegato, setGoogleCollegato] = useState<boolean | null>(null);
  const [connettendo, setConnettendo] = useState(false);
  const [erroreGoogle, setErroreGoogle] = useState<string | null>(null);

  const [passwordAttuale, setPasswordAttuale] = useState("");
  const [nuovaPassword, setNuovaPassword] = useState("");
  const [erroreCambio, setErroreCambio] = useState<string | null>(null);
  const [successoCambio, setSuccessoCambio] = useState(false);
  const [inviandoCambio, setInviandoCambio] = useState(false);

  useEffect(() => {
    if (!mostraGoogle) return;
    api.googleCalendar.stato().then((r) => setGoogleCollegato(r.collegato)).catch(() => setGoogleCollegato(false));
  }, [mostraGoogle]);

  async function handleConnetti() {
    setConnettendo(true);
    setErroreGoogle(null);
    try {
      const { url } = await api.googleCalendar.connetti();
      window.location.href = url;
    } catch (err) {
      setErroreGoogle(messaggioErrore(err));
      setConnettendo(false);
    }
  }

  async function handleCambiaPassword(e: FormEvent) {
    e.preventDefault();
    setErroreCambio(null);
    setSuccessoCambio(false);
    setInviandoCambio(true);
    try {
      await api.auth.cambiaPassword(passwordAttuale, nuovaPassword);
      setSuccessoCambio(true);
      setPasswordAttuale("");
      setNuovaPassword("");
    } catch (err) {
      setErroreCambio(messaggioErrore(err));
    } finally {
      setInviandoCambio(false);
    }
  }

  return (
    <div className="max-w-lg space-y-6">
      <PageHeader title="Impostazioni" />

      {mostraGoogle && (
      <Card>
        <p className="mb-1 text-sm font-medium text-zinc-700">Google Calendar</p>
        <p className="mb-3 text-sm text-zinc-500">
          Collega il tuo account Google per sincronizzare le scadenze delle attività con il tuo calendario.
        </p>

        {erroreGoogle && <div className="mb-3"><ErrorBlock message={erroreGoogle} /></div>}

        {googleCollegato === null ? null : googleCollegato ? (
          <Badge tone="green">Collegato</Badge>
        ) : (
          <Button onClick={handleConnetti} disabled={connettendo}>
            {connettendo ? "Reindirizzamento…" : "Collega Google Calendar"}
          </Button>
        )}
      </Card>
      )}

      <Card>
        <p className="mb-3 text-sm font-medium text-zinc-700">Cambia password</p>
        <form onSubmit={handleCambiaPassword} className="space-y-3">
          <Field label="Password attuale">
            <Input type="password" required value={passwordAttuale} onChange={(e) => setPasswordAttuale(e.target.value)} />
          </Field>
          <Field label="Nuova password">
            <Input type="password" required minLength={8} value={nuovaPassword} onChange={(e) => setNuovaPassword(e.target.value)} />
          </Field>

          {erroreCambio && <ErrorBlock message={erroreCambio} />}
          {successoCambio && <p className="text-sm text-emerald-700">Password aggiornata con successo.</p>}

          <Button type="submit" disabled={inviandoCambio}>{inviandoCambio ? "Salvataggio…" : "Aggiorna password"}</Button>
        </form>
      </Card>
    </div>
  );
}
