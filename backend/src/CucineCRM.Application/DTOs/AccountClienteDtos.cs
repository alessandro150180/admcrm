namespace CucineCRM.Application.DTOs;

/// <summary>Credenziali appena generate per un cliente. La password in chiaro esiste solo in questa
/// risposta (nel database resta solo l'hash): serve a consegnarla al cliente, non è recuperabile dopo.</summary>
public record AccountClienteGeneratoDto(
    int ClienteId,
    string CodiceCliente,
    string RagioneSociale,
    string Email,
    string Password,
    string? Nota
);
