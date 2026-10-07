using CucineCRM.Domain.Enums;

namespace CucineCRM.Application.DTOs;

public record LoginRequestDto(string Email, string Password);

public record LoginResponseDto(
    string Token,
    DateTime ScadenzaToken,
    UtenteDto Utente
);

public record UtenteDto(
    int Id,
    string Nome,
    string Cognome,
    string Email,
    RuoloUtente Ruolo,
    bool Attivo,
    int? AgenteId,
    int? ClienteId
);

public record CreaUtenteDto(
    string Nome,
    string Cognome,
    string Email,
    string Password,
    RuoloUtente Ruolo,
    int? AgenteId,
    int? ClienteId = null
);

public record CambiaPasswordDto(string PasswordAttuale, string NuovaPassword);
