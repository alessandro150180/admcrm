using CucineCRM.Domain.Entities;
using CucineCRM.Domain.Enums;

namespace CucineCRM.Application.Interfaces;

/// <summary>Hashing/verifica password con BCrypt (implementato in Infrastructure).</summary>
public interface IPasswordHasher
{
    string Hash(string password);
    bool Verify(string password, string hash);
}

/// <summary>Generazione del JWT dopo login riuscito.</summary>
public interface IJwtTokenGenerator
{
    string GenerateToken(Utente utente);
}

/// <summary>
/// Espone i dati dell'utente autenticato nella request corrente (popolato dal middleware JWT).
/// Usato dai servizi Application per applicare la scoping dei dati (vedi ScopingService).
/// </summary>
public interface ICurrentUserService
{
    int? UtenteId { get; }
    RuoloUtente? Ruolo { get; }
    int? AgenteId { get; } // valorizzato solo se l'utente è collegato a un Agente
    int? ClienteId { get; } // valorizzato solo per gli account Cliente
    bool IsAuthenticated { get; }
}
