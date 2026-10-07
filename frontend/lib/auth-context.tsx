"use client";

import { createContext, useContext, useEffect, useState, type ReactNode } from "react";
import { useRouter } from "next/navigation";
import { api, clearSessione, getToken, getUtenteSalvato, setSessione } from "./api";
import type { UtenteDto } from "./types";

interface AuthContextValue {
  utente: UtenteDto | null;
  caricato: boolean;
  login: (email: string, password: string) => Promise<void>;
  logout: () => void;
}

const AuthContext = createContext<AuthContextValue | null>(null);

export function AuthProvider({ children }: { children: ReactNode }) {
  const [utente, setUtente] = useState<UtenteDto | null>(null);
  const [caricato, setCaricato] = useState(false);
  const router = useRouter();

  useEffect(() => {
    if (getToken()) {
      setUtente(getUtenteSalvato());
    }
    setCaricato(true);
  }, []);

  async function login(email: string, password: string) {
    const risposta = await api.auth.login(email, password);
    setSessione(risposta.token, risposta.utente);
    setUtente(risposta.utente);
  }

  function logout() {
    clearSessione();
    setUtente(null);
    router.push("/login");
  }

  return (
    <AuthContext.Provider value={{ utente, caricato, login, logout }}>
      {children}
    </AuthContext.Provider>
  );
}

export function useAuth(): AuthContextValue {
  const ctx = useContext(AuthContext);
  if (!ctx) throw new Error("useAuth deve essere usato dentro <AuthProvider>.");
  return ctx;
}

/** Ruoli con visibilità gestionale (creare clienti, importare, vedere audit log...). */
export function isDirezioneOAreaManager(ruolo: string | undefined) {
  return ruolo === "Amministratore" || ruolo === "DirettoreCommerciale" || ruolo === "AreaManager";
}

export function isSoloDirezione(ruolo: string | undefined) {
  return ruolo === "Amministratore" || ruolo === "DirettoreCommerciale";
}

/** Ruolo di sola visualizzazione: vede tutto come l'Amministratore ma non può modificare nulla. */
export function isSolaLettura(ruolo: string | undefined) {
  return ruolo === "Visualizzatore";
}

/** Account di un cliente finale: vede solo il proprio fatturato e i propri ordini, in sola lettura. */
export function isCliente(ruolo: string | undefined) {
  return ruolo === "Cliente";
}

/** Vero se il ruolo può creare/modificare/eliminare dati (tutti tranne Visualizzatore e Cliente). */
export function puoModificare(ruolo: string | undefined) {
  return !isSolaLettura(ruolo) && !isCliente(ruolo);
}

/** Quota di provvigione riservata alla Ditta ADM: dato riservato alla direzione, invisibile agli agenti. */
export function puoVedereQuotaAdm(ruolo: string | undefined) {
  return isSoloDirezione(ruolo) || isSolaLettura(ruolo);
}
