namespace CucineCRM.Domain.Enums;

public enum RuoloUtente
{
    Amministratore = 0,
    DirettoreCommerciale = 1,
    AreaManager = 2,
    Agente = 3,

    // Visibilità completa su tutti gli agenti/clienti/fatturati come l'Amministratore,
    // ma accesso di sola lettura: nessuna creazione, modifica o cancellazione è permessa
    // (imposto a livello di middleware globale, non per singolo endpoint).
    Visualizzatore = 4,

    // Account di un cliente finale: sola lettura, vincolato al proprio record Cliente (ClienteId
    // sull'Utente). Vede solo il proprio fatturato/ordini per tutti i fornitori; niente provvigioni,
    // agenti, obiettivi, note interne né dati di altri clienti.
    Cliente = 5
}

public enum StatoOrdine
{
    InAttesa = 0,
    Confermato = 1,
    InProduzione = 2,
    Spedito = 3,
    Consegnato = 4,
    Annullato = 5
}

public enum TipoAttivita
{
    Telefonata = 0,
    Visita = 1,
    Preventivo = 2,
    FollowUp = 3,
    Reclamo = 4,
    Campionario = 5,
    Email = 6,
    Assistenza = 7
}

public enum PrioritaAttivita
{
    Bassa = 0,
    Media = 1,
    Alta = 2,
    Urgente = 3
}

public enum StatoAttivita
{
    DaFare = 0,
    InCorso = 1,
    Completata = 2,
    Annullata = 3
}

/// <summary>A chi è rivolta una comunicazione: determina chi la vede e può scaricarla.</summary>
public enum DestinatariComunicazione
{
    Tutti = 0,       // rete vendita (agenti e area manager) e clienti
    SoloAgenti = 1,  // solo la rete vendita
    SoloClienti = 2  // solo i clienti
}
