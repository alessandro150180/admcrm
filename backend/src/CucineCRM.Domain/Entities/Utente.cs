using CucineCRM.Domain.Common;
using CucineCRM.Domain.Enums;

namespace CucineCRM.Domain.Entities;

/// <summary>
/// Utente del sistema (login). Un Utente con ruolo Agente è collegato 1:1 a un record Agente
/// tramite AgenteId, che porta i dati commerciali (zona, clienti assegnati, ecc.).
/// </summary>
public class Utente : BaseEntity
{
    public string Nome { get; set; } = string.Empty;
    public string Cognome { get; set; } = string.Empty;
    public string Email { get; set; } = string.Empty;
    public string PasswordHash { get; set; } = string.Empty; // BCrypt
    public RuoloUtente Ruolo { get; set; }
    public bool Attivo { get; set; } = true;

    // FK opzionale: valorizzata solo per utenti con ruolo Agente o AreaManager
    public int? AgenteId { get; set; }
    public Agente? Agente { get; set; }

    // FK opzionale: valorizzata solo per utenti con ruolo Cliente (account del cliente finale)
    public int? ClienteId { get; set; }
    public Cliente? Cliente { get; set; }

    // Token OAuth Google Calendar (nulli finché l'utente non collega il proprio account)
    public string? GoogleAccessToken { get; set; }
    public string? GoogleRefreshToken { get; set; }
    public DateTime? GoogleTokenScadenza { get; set; }

    // Navigazione inversa
    public ICollection<Attivita> Attivita { get; set; } = new List<Attivita>();
    public ICollection<NotaCliente> Note { get; set; } = new List<NotaCliente>();
    public ICollection<Importazione> Importazioni { get; set; } = new List<Importazione>();
    public ICollection<Comunicazione> Comunicazioni { get; set; } = new List<Comunicazione>();
}
