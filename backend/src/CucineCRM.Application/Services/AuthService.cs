using CucineCRM.Application.Common;
using CucineCRM.Application.DTOs;
using CucineCRM.Application.Interfaces;
using CucineCRM.Domain.Entities;
using CucineCRM.Domain.Enums;

namespace CucineCRM.Application.Services;

/// <summary>
/// Logica applicativa di autenticazione. Non conosce EF Core, BCrypt o JWT concretamente:
/// riceve tutto tramite interfacce iniettate (Dependency Inversion, principio D di SOLID).
/// </summary>
public class AuthService : IAuthService
{
    private readonly IUnitOfWork _unitOfWork;
    private readonly IPasswordHasher _passwordHasher;
    private readonly IJwtTokenGenerator _jwtTokenGenerator;

    public AuthService(
        IUnitOfWork unitOfWork,
        IPasswordHasher passwordHasher,
        IJwtTokenGenerator jwtTokenGenerator)
    {
        _unitOfWork = unitOfWork;
        _passwordHasher = passwordHasher;
        _jwtTokenGenerator = jwtTokenGenerator;
    }

    public async Task<LoginResponseDto> LoginAsync(LoginRequestDto request, CancellationToken ct = default)
    {
        var utente = (await _unitOfWork.Utenti.FindAsync(u => u.Email == request.Email && !u.Eliminato, ct))
            .FirstOrDefault();

        if (utente is null || !_passwordHasher.Verify(request.Password, utente.PasswordHash))
            throw new AuthenticationException("Email o password non corretti.");

        if (!utente.Attivo)
            throw new AuthenticationException("Utente disattivato. Contattare l'amministratore.");

        var token = _jwtTokenGenerator.GenerateToken(utente);

        return new LoginResponseDto(
            Token: token,
            ScadenzaToken: DateTime.UtcNow.AddHours(8),
            Utente: MapToDto(utente)
        );
    }

    public async Task<UtenteDto> CreaUtenteAsync(CreaUtenteDto request, CancellationToken ct = default)
    {
        var esistente = (await _unitOfWork.Utenti.FindAsync(u => u.Email == request.Email, ct)).Any();
        if (esistente)
            throw new ValidationAppException($"Esiste già un utente con email '{request.Email}'.");

        if (request.Ruolo == RuoloUtente.Cliente)
        {
            if (request.ClienteId is null)
                throw new ValidationAppException("Un account Cliente deve essere collegato a un cliente.");

            if (await _unitOfWork.Clienti.GetByIdAsync(request.ClienteId.Value, ct) is null)
                throw new NotFoundException(nameof(Cliente), request.ClienteId.Value);

            if ((await _unitOfWork.Utenti.FindAsync(u => u.ClienteId == request.ClienteId, ct)).Any())
                throw new ValidationAppException("Questo cliente ha già un account.");
        }

        var utente = new Utente
        {
            Nome = request.Nome,
            Cognome = request.Cognome,
            Email = request.Email,
            PasswordHash = _passwordHasher.Hash(request.Password),
            Ruolo = request.Ruolo,
            AgenteId = request.AgenteId,
            ClienteId = request.Ruolo == RuoloUtente.Cliente ? request.ClienteId : null,
            Attivo = true
        };

        await _unitOfWork.Utenti.AddAsync(utente, ct);
        await _unitOfWork.SaveChangesAsync(ct);

        return MapToDto(utente);
    }

    public async Task CambiaPasswordAsync(int utenteId, CambiaPasswordDto request, CancellationToken ct = default)
    {
        var utente = await _unitOfWork.Utenti.GetByIdAsync(utenteId, ct)
            ?? throw new NotFoundException(nameof(Utente), utenteId);

        if (!_passwordHasher.Verify(request.PasswordAttuale, utente.PasswordHash))
            throw new AuthenticationException("Password attuale non corretta.");

        utente.PasswordHash = _passwordHasher.Hash(request.NuovaPassword);
        _unitOfWork.Utenti.Update(utente);
        await _unitOfWork.SaveChangesAsync(ct);
    }

    private static UtenteDto MapToDto(Utente u) =>
        new(u.Id, u.Nome, u.Cognome, u.Email, u.Ruolo, u.Attivo, u.AgenteId, u.ClienteId);
}
