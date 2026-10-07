import type {
  AgenteDto,
  AttivitaDto,
  AuditLogDto,
  ClienteDettaglioDto,
  ClienteDto,
  ComunicazioneDto,
  DestinatariComunicazione,
  DashboardKpiDto,
  FornitoreDto,
  ImportazioneRisultatoDto,
  LoginResponseDto,
  NotaClienteDto,
  NotificaDto,
  ObiettivoVenditaDto,
  OrdineDto,
  PagedResult,
  ProvvigioneClienteDto,
  PuntoGraficoMensileDto,
  UtenteDto,
} from "./types";

// In sviluppo locale NEXT_PUBLIC_API_URL è impostata in .env.local (backend su localhost:5080).
// In produzione (Vercel) resta intenzionalmente non impostata: le chiamate usano un percorso
// relativo, gestito dal rewrite in next.config.ts verso il backend su Render — così il browser
// non fa mai una richiesta cross-origin ed evita il problema dei CORS header persi da Render.
const BASE_URL = process.env.NEXT_PUBLIC_API_URL ?? "";
const TOKEN_KEY = "cucinecrm_token";
const UTENTE_KEY = "cucinecrm_utente";

export class ApiError extends Error {
  status: number;
  errors?: Record<string, string[]>;

  constructor(message: string, status: number, errors?: Record<string, string[]>) {
    super(message);
    this.status = status;
    this.errors = errors;
  }
}

export function getToken(): string | null {
  if (typeof window === "undefined") return null;
  return localStorage.getItem(TOKEN_KEY);
}

export function setSessione(token: string, utente: UtenteDto) {
  if (typeof window === "undefined") return;
  localStorage.setItem(TOKEN_KEY, token);
  localStorage.setItem(UTENTE_KEY, JSON.stringify(utente));
}

export function getUtenteSalvato(): UtenteDto | null {
  if (typeof window === "undefined") return null;
  const raw = localStorage.getItem(UTENTE_KEY);
  return raw ? (JSON.parse(raw) as UtenteDto) : null;
}

export function clearSessione() {
  if (typeof window === "undefined") return;
  localStorage.removeItem(TOKEN_KEY);
  localStorage.removeItem(UTENTE_KEY);
}

interface ApiFetchOptions extends RequestInit {
  skipAuthRedirect?: boolean;
}

async function apiFetch<T>(path: string, options: ApiFetchOptions = {}): Promise<T> {
  const token = getToken();
  const headers = new Headers(options.headers);
  const isFormData = options.body instanceof FormData;
  if (!isFormData && !headers.has("Content-Type")) {
    headers.set("Content-Type", "application/json");
  }
  if (token) headers.set("Authorization", `Bearer ${token}`);

  const response = await fetch(`${BASE_URL}${path}`, { ...options, headers });

  if (response.status === 401 && !options.skipAuthRedirect) {
    clearSessione();
    if (typeof window !== "undefined" && window.location.pathname !== "/login") {
      window.location.href = "/login";
    }
    throw new ApiError("Sessione scaduta: effettua di nuovo il login.", 401);
  }

  if (response.status === 204) {
    return undefined as T;
  }

  const contentType = response.headers.get("content-type") ?? "";
  const body = contentType.includes("application/json") ? await response.json() : undefined;

  if (!response.ok) {
    const message = body?.detail ?? body?.title ?? `Errore ${response.status}`;
    throw new ApiError(message, response.status, body?.errors);
  }

  return body as T;
}

async function apiFetchBlob(path: string, options: RequestInit = {}): Promise<Blob> {
  const token = getToken();
  const headers = new Headers(options.headers);
  if (token) headers.set("Authorization", `Bearer ${token}`);

  const response = await fetch(`${BASE_URL}${path}`, { ...options, headers });
  if (!response.ok) {
    throw new ApiError(`Errore ${response.status} durante il download.`, response.status);
  }
  return response.blob();
}

export function scaricaBlob(blob: Blob, nomeFile: string) {
  const url = URL.createObjectURL(blob);
  const a = document.createElement("a");
  a.href = url;
  a.download = nomeFile;
  document.body.appendChild(a);
  a.click();
  a.remove();
  URL.revokeObjectURL(url);
}

// Il parametro è tipizzato come "object" (non Record<string, ...>) apposta: passare un'interfaccia
// concreta come FiltriLista a un parametro Record<string, X> fallisce il controllo dei tipi di
// TypeScript perché le interfacce senza index signature esplicita non sono strutturalmente
// compatibili con Record. Il cast è quindi interno alla funzione, non nella firma pubblica.
function buildQuery(params: object): string {
  const search = new URLSearchParams();
  for (const [key, value] of Object.entries(params as Record<string, string | number | boolean | undefined | null | number[]>)) {
    if (value === undefined || value === null || value === "") continue;
    if (Array.isArray(value)) {
      // ASP.NET Core lega un array di query string a int[] solo con la stessa chiave ripetuta
      // (?mesi=6&mesi=7), non con valori separati da virgola.
      for (const elemento of value) search.append(key, String(elemento));
    } else {
      search.set(key, String(value));
    }
  }
  const qs = search.toString();
  return qs ? `?${qs}` : "";
}

export interface FiltriLista {
  pagina?: number;
  dimensione?: number;
  regione?: string;
  provincia?: string;
  agenteId?: number;
  anno?: number;
  mese?: number;
  fornitoreIds?: number[];
}

export const api = {
  auth: {
    login: (email: string, password: string) =>
      apiFetch<LoginResponseDto>("/api/auth/login", {
        method: "POST",
        body: JSON.stringify({ email, password }),
        skipAuthRedirect: true,
      }),
    me: () => apiFetch<{ id: string; email: string; nome: string; ruolo: string; agenteId: string | null }>("/api/auth/me"),
    cambiaPassword: (passwordAttuale: string, nuovaPassword: string) =>
      apiFetch<void>("/api/auth/cambia-password", {
        method: "POST",
        body: JSON.stringify({ passwordAttuale, nuovaPassword }),
      }),
  },

  agenti: {
    lista: () => apiFetch<AgenteDto[]>("/api/agenti"),
    crea: (payload: { nome: string; cognome: string; zona: string; telefono?: string; email: string; areaManagerId?: number }) =>
      apiFetch<AgenteDto>("/api/agenti", { method: "POST", body: JSON.stringify(payload) }),
    elimina: (id: number) => apiFetch<void>(`/api/agenti/${id}`, { method: "DELETE" }),
  },

  dashboard: {
    kpi: (mesi: number[], anno: number, agenteId?: number, clienteId?: number, fornitoreIds?: number[]) =>
      apiFetch<DashboardKpiDto>(`/api/dashboard/kpi${buildQuery({ mesi, anno, agenteId, clienteId, fornitoreIds })}`),
    fatturatoMensile: (anno: number, agenteId?: number, clienteId?: number, fornitoreIds?: number[]) =>
      apiFetch<PuntoGraficoMensileDto[]>(`/api/dashboard/fatturato-mensile${buildQuery({ anno, agenteId, clienteId, fornitoreIds })}`),
    provvigioni: (mesi: number[], anno: number, agenteId?: number, clienteId?: number, fornitoreIds?: number[]) =>
      apiFetch<ProvvigioneClienteDto[]>(`/api/dashboard/provvigioni${buildQuery({ mesi, anno, agenteId, clienteId, fornitoreIds })}`),
  },

  fornitori: {
    lista: () => apiFetch<FornitoreDto[]>("/api/fornitori"),
    crea: (nome: string) => apiFetch<FornitoreDto>("/api/fornitori", { method: "POST", body: JSON.stringify({ nome }) }),
  },

  clienti: {
    lista: (filtri: FiltriLista) => apiFetch<PagedResult<ClienteDto>>(`/api/clienti${buildQuery(filtri)}`),
    dettaglio: (id: number) => apiFetch<ClienteDettaglioDto>(`/api/clienti/${id}`),
    crea: (payload: {
      ragioneSociale: string; codiceCliente: string; partitaIVA?: string; indirizzo?: string;
      citta?: string; provincia?: string; regione?: string; cap?: string; telefono?: string;
      email?: string; agenteId: number; percentualeProvvigione?: number;
    }) => apiFetch<ClienteDto>("/api/clienti", { method: "POST", body: JSON.stringify(payload) }),
    note: (id: number) => apiFetch<NotaClienteDto[]>(`/api/clienti/${id}/note`),
    aggiungiNota: (id: number, testo: string) =>
      apiFetch<NotaClienteDto>(`/api/clienti/${id}/note`, { method: "POST", body: JSON.stringify({ testo }) }),
    impostaProvvigione: (id: number, percentualeProvvigione: number) =>
      apiFetch<ClienteDto>(`/api/clienti/${id}/provvigione`, { method: "PUT", body: JSON.stringify({ percentualeProvvigione }) }),
    esportaCsv: (filtri: FiltriLista) =>
      apiFetchBlob(`/api/clienti/export/csv${buildQuery(filtri)}`),
    esportaPdf: (id: number) => apiFetchBlob(`/api/clienti/${id}/export/pdf`),
  },

  ordini: {
    lista: (filtri: FiltriLista) => apiFetch<PagedResult<OrdineDto>>(`/api/ordini${buildQuery(filtri)}`),
    dettaglio: (id: number) => apiFetch<OrdineDto>(`/api/ordini/${id}`),
    crea: (payload: {
      clienteId: number; fornitoreId: number; dataOrdine: string; importo: number; numeroCucine: number;
      numeroElettrodomestici: number; numeroComplementi: number; riferimentoEsterno?: string;
    }) => apiFetch<OrdineDto>("/api/ordini", { method: "POST", body: JSON.stringify(payload) }),
    aggiornaStato: (id: number, nuovoStato: string) =>
      apiFetch<OrdineDto>(`/api/ordini/${id}/stato`, { method: "PATCH", body: JSON.stringify({ nuovoStato }) }),
    esportaCsv: (filtri: FiltriLista) => apiFetchBlob(`/api/ordini/export/csv${buildQuery(filtri)}`),
  },

  attivita: {
    lista: (filtri: { pagina?: number; dimensione?: number; agenteId?: number; stato?: string; soloScadute?: boolean }) =>
      apiFetch<PagedResult<AttivitaDto>>(`/api/attivita${buildQuery(filtri)}`),
    dettaglio: (id: number) => apiFetch<AttivitaDto>(`/api/attivita/${id}`),
    crea: (payload: {
      clienteId: number; tipoAttivita: string; titolo: string; descrizione?: string;
      priorita: string; dataScadenza: string;
    }) => apiFetch<AttivitaDto>("/api/attivita", { method: "POST", body: JSON.stringify(payload) }),
    aggiornaStato: (id: number, nuovoStato: string) =>
      apiFetch<AttivitaDto>(`/api/attivita/${id}/stato`, { method: "PATCH", body: JSON.stringify({ nuovoStato }) }),
    sincronizzaCalendario: (id: number) =>
      apiFetch<{ googleEventId: string }>(`/api/attivita/${id}/sincronizza-calendario`, { method: "POST" }),
  },

  obiettivi: {
    lista: (anno: number, agenteId?: number) =>
      apiFetch<ObiettivoVenditaDto[]>(`/api/obiettivivendita${buildQuery({ anno, agenteId })}`),
    imposta: (payload: { agenteId: number; mese: number; anno: number; obiettivoFatturato: number; obiettivoCucine: number }) =>
      apiFetch<ObiettivoVenditaDto>("/api/obiettivivendita", { method: "PUT", body: JSON.stringify(payload) }),
  },

  importazioni: {
    importaOrdini: (file: File, periodoCompetenza: string, fornitoreId: number) => {
      const formData = new FormData();
      formData.append("file", file);
      formData.append("periodoCompetenza", periodoCompetenza);
      formData.append("fornitoreId", String(fornitoreId));
      return apiFetch<ImportazioneRisultatoDto>("/api/importazioni/ordini", { method: "POST", body: formData });
    },
    importaClienti: (file: File, periodoCompetenza: string) => {
      const formData = new FormData();
      formData.append("file", file);
      formData.append("periodoCompetenza", periodoCompetenza);
      return apiFetch<ImportazioneRisultatoDto>("/api/importazioni/clienti", { method: "POST", body: formData });
    },
    importaFatturatoMensile: (file: File, fornitoreId: number) => {
      const formData = new FormData();
      formData.append("file", file);
      formData.append("fornitoreId", String(fornitoreId));
      return apiFetch<ImportazioneRisultatoDto>("/api/importazioni/fatturato-mensile", { method: "POST", body: formData });
    },
    eliminaFatturatoMensile: (anno: number, mese: number, fornitoreId: number) =>
      apiFetch<{ ordiniEliminati: number }>(`/api/importazioni/fatturato-mensile/${anno}/${mese}/${fornitoreId}`, { method: "DELETE" }),
  },

  comunicazioni: {
    lista: () => apiFetch<ComunicazioneDto[]>("/api/comunicazioni"),
    scarica: (id: number) => apiFetchBlob(`/api/comunicazioni/${id}/download`),
    crea: (file: File, titolo: string, descrizione: string, destinatari: DestinatariComunicazione) => {
      const formData = new FormData();
      formData.append("file", file);
      formData.append("titolo", titolo);
      formData.append("destinatari", destinatari);
      if (descrizione) formData.append("descrizione", descrizione);
      return apiFetch<ComunicazioneDto>("/api/comunicazioni", { method: "POST", body: formData });
    },
    aggiornaDestinatari: (id: number, destinatari: DestinatariComunicazione) =>
      apiFetch<void>(`/api/comunicazioni/${id}/destinatari`, { method: "PATCH", body: JSON.stringify({ destinatari }) }),
    elimina: (id: number) => apiFetch<void>(`/api/comunicazioni/${id}`, { method: "DELETE" }),
  },

  notifiche: {
    lista: (soloNonLette?: boolean) =>
      apiFetch<NotificaDto[]>(`/api/notifiche${buildQuery({ soloNonLette })}`),
    segnaComeLetta: (id: number) => apiFetch<void>(`/api/notifiche/${id}/letta`, { method: "PATCH" }),
    generaScadute: () => apiFetch<{ notificheGenerate: number }>("/api/notifiche/genera-scadute", { method: "POST" }),
  },

  auditLog: {
    lista: (filtri: { pagina?: number; dimensione?: number; nomeEntita?: string; entitaId?: number; utenteId?: number }) =>
      apiFetch<PagedResult<AuditLogDto>>(`/api/auditlog${buildQuery(filtri)}`),
  },

  googleCalendar: {
    connetti: () => apiFetch<{ url: string }>("/api/google-calendar/connetti"),
    stato: () => apiFetch<{ collegato: boolean }>("/api/google-calendar/stato"),
  },
};
