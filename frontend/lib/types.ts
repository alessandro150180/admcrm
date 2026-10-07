// Tipi TypeScript che rispecchiano i DTO del backend (CucineCRM.Application.DTOs).
// Gli enum viaggiano come stringhe (JsonStringEnumConverter lato API), non come numeri.

export type RuoloUtente =
  | "Amministratore"
  | "DirettoreCommerciale"
  | "AreaManager"
  | "Agente"
  | "Visualizzatore"
  | "Cliente";

export type StatoOrdine =
  | "InAttesa"
  | "Confermato"
  | "InProduzione"
  | "Spedito"
  | "Consegnato"
  | "Annullato";

export type TipoAttivita =
  | "Telefonata"
  | "Visita"
  | "Preventivo"
  | "FollowUp"
  | "Reclamo"
  | "Campionario"
  | "Email"
  | "Assistenza";

export type PrioritaAttivita = "Bassa" | "Media" | "Alta" | "Urgente";

export type StatoAttivita = "DaFare" | "InCorso" | "Completata" | "Annullata";

export interface UtenteDto {
  id: number;
  nome: string;
  cognome: string;
  email: string;
  ruolo: RuoloUtente;
  attivo: boolean;
  agenteId: number | null;
  clienteId: number | null;
}

export interface LoginResponseDto {
  token: string;
  scadenzaToken: string;
  utente: UtenteDto;
}

export interface PagedResult<T> {
  elementi: T[];
  pagina: number;
  dimensione: number;
  totaleElementi: number;
  totalePagine: number;
}

export interface FornitoreDto {
  id: number;
  nome: string;
  attivo: boolean;
}

export interface AgenteDto {
  id: number;
  nome: string;
  cognome: string;
  zona: string;
  telefono: string | null;
  email: string;
  areaManagerId: number | null;
}

export interface ClienteDto {
  id: number;
  ragioneSociale: string;
  codiceCliente: string;
  partitaIVA: string | null;
  indirizzo: string | null;
  citta: string | null;
  provincia: string | null;
  regione: string | null;
  cap: string | null;
  telefono: string | null;
  email: string | null;
  agenteId: number;
  agenteNomeCompleto: string;
  agenteEmail: string;
  dataInserimento: string;
  percentualeProvvigione: number;
}

export interface ClienteDettaglioDto {
  anagrafica: ClienteDto;
  numeroOrdiniTotali: number;
  fatturatoTotale: number;
  numeroCucineAcquistate: number;
  numeroElettrodomesticiAcquistati: number;
  ordineMedio: number;
  ultimoAcquisto: string | null;
}

export interface NotaClienteDto {
  id: number;
  clienteId: number;
  utenteId: number;
  utenteNomeCompleto: string;
  testo: string;
  dataInserimento: string;
}

export interface OrdineDto {
  id: number;
  clienteId: number;
  clienteRagioneSociale: string;
  fornitoreId: number;
  fornitoreNome: string;
  dataOrdine: string;
  importo: number;
  numeroCucine: number;
  numeroElettrodomestici: number;
  numeroComplementi: number;
  statoOrdine: StatoOrdine;
  riferimentoEsterno: string | null;
}

export interface AttivitaDto {
  id: number;
  clienteId: number;
  clienteRagioneSociale: string;
  utenteId: number;
  utenteNomeCompleto: string;
  tipoAttivita: TipoAttivita;
  titolo: string;
  descrizione: string | null;
  priorita: PrioritaAttivita;
  dataScadenza: string;
  completata: boolean;
  stato: StatoAttivita;
}

export interface ObiettivoVenditaDto {
  id: number;
  agenteId: number;
  agenteNomeCompleto: string;
  mese: number;
  anno: number;
  obiettivoFatturato: number;
  obiettivoCucine: number;
  fatturatoRealizzato: number;
  percentualeRaggiungimento: number;
}

export interface KpiDto {
  valoreCorrente: number;
  valoreAnnoPrecedente: number;
  differenzaPercentuale: number;
  trendPositivo: boolean;
}

export interface DashboardKpiDto {
  fatturatoMensile: KpiDto;
  nuoviClienti: KpiDto;
  ordineMedio: KpiDto;
  cucineVendute: KpiDto;
}

export interface PuntoGraficoMensileDto {
  mese: number;
  anno: number;
  valore: number;
}

export interface ProvvigioneClienteDto {
  clienteId: number;
  ragioneSociale: string;
  agenteId: number;
  agenteNomeCompleto: string;
  fatturato: number;
  percentualeProvvigione: number;
  importoProvvigione: number;
  percentualeProvvigioneAdm: number;
  importoProvvigioneAdm: number;
  differenzaAdmAgente: number;
}

export type DestinatariComunicazione = "Tutti" | "SoloAgenti" | "SoloClienti";

export interface ComunicazioneDto {
  id: number;
  titolo: string;
  descrizione: string | null;
  nomeFile: string;
  tipoContenuto: string;
  dimensioneByte: number;
  dataPubblicazione: string;
  utentePubblicazioneNomeCompleto: string;
  destinatari: DestinatariComunicazione;
}

export interface ImportazioneRisultatoDto {
  id: number;
  nomeFile: string;
  dataImportazione: string;
  periodoCompetenza: string;
  righePlesse: number;
  righeImportate: number;
  righeScartate: number;
  righeDuplicate: number;
  completata: boolean;
  logEsito: string | null;
}

export interface RigaImportLogDto {
  numeroRiga: number;
  esito: string;
  messaggio: string | null;
}

export interface NotificaDto {
  id: number;
  tipo: string;
  titolo: string;
  messaggio: string | null;
  riferimentoEntitaId: number | null;
  letta: boolean;
  dataCreazione: string;
}

export interface AuditLogDto {
  id: number;
  utenteId: number | null;
  utenteNomeCompleto: string | null;
  nomeEntita: string;
  entitaId: number;
  azione: string;
  dataCreazione: string;
}

export interface ApiErrorBody {
  title?: string;
  status?: number;
  detail?: string;
  errors?: Record<string, string[]>;
}
