using CucineCRM.Domain.Common;
using CucineCRM.Domain.Enums;

namespace CucineCRM.Domain.Entities;

/// <summary>
/// Circolare, PDF o file Excel pubblicato dalla direzione, visibile e scaricabile da tutta la rete
/// vendita. Il contenuto del file è salvato direttamente nel database (nessuno storage esterno):
/// per documenti aziendali di dimensioni contenute è la soluzione più semplice e affidabile.
/// </summary>
public class Comunicazione : BaseEntity
{
    public string Titolo { get; set; } = string.Empty;
    public string? Descrizione { get; set; }

    public string NomeFile { get; set; } = string.Empty;
    public string TipoContenuto { get; set; } = string.Empty; // MIME type, es. "application/pdf"
    public long DimensioneByte { get; set; }
    public byte[] Contenuto { get; set; } = Array.Empty<byte>();

    // Chi può vederla e scaricarla (la direzione e il Visualizzatore vedono sempre tutto).
    public DestinatariComunicazione Destinatari { get; set; } = DestinatariComunicazione.Tutti;

    public int UtentePubblicazioneId { get; set; }
    public Utente UtentePubblicazione { get; set; } = null!;
}
