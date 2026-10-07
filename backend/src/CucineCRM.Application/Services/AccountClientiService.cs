using System.Security.Cryptography;
using CucineCRM.Application.DTOs;
using CucineCRM.Application.Interfaces;
using CucineCRM.Domain.Entities;
using CucineCRM.Domain.Enums;

namespace CucineCRM.Application.Services;

public class AccountClientiService : IAccountClientiService
{
    // Senza caratteri ambigui (0/O, 1/l/I): le password vengono lette e ricopiate a mano dai clienti.
    private const string Alfabeto = "ABCDEFGHJKLMNPQRSTUVWXYZabcdefghijkmnpqrstuvwxyz23456789";
    private const int LunghezzaPassword = 12;
    private const string DominioFittizio = "clienti.admcrm.invalid"; // .invalid è un TLD riservato: non può esistere davvero

    private readonly IUnitOfWork _unitOfWork;
    private readonly IAsyncQueryExecutor _queryExecutor;
    private readonly IPasswordHasher _passwordHasher;

    public AccountClientiService(IUnitOfWork unitOfWork, IAsyncQueryExecutor queryExecutor, IPasswordHasher passwordHasher)
    {
        _unitOfWork = unitOfWork;
        _queryExecutor = queryExecutor;
        _passwordHasher = passwordHasher;
    }

    public async Task<IReadOnlyList<AccountClienteGeneratoDto>> GeneraAccountMancantiAsync(CancellationToken ct = default)
    {
        var utenti = await _queryExecutor.ToListAsync(_unitOfWork.Utenti.Query().Select(u => new { u.Email, u.ClienteId }), ct);
        var emailUsate = new HashSet<string>(utenti.Select(u => u.Email), StringComparer.OrdinalIgnoreCase);
        var clientiConAccount = utenti.Where(u => u.ClienteId.HasValue).Select(u => u.ClienteId!.Value).ToHashSet();

        var clienti = await _queryExecutor.ToListAsync(_unitOfWork.Clienti.Query()
            .OrderBy(c => c.RagioneSociale)
            .Select(c => new { c.Id, c.CodiceCliente, c.RagioneSociale, c.Email }), ct);

        var generati = new List<AccountClienteGeneratoDto>();

        foreach (var cliente in clienti.Where(c => !clientiConAccount.Contains(c.Id)))
        {
            var (email, nota) = ScegliEmail(cliente.Email, cliente.CodiceCliente, emailUsate);
            emailUsate.Add(email);
            var password = GeneraPassword();

            await _unitOfWork.Utenti.AddAsync(new Utente
            {
                Nome = Tronca(cliente.RagioneSociale, 100),
                Cognome = Tronca($"({cliente.CodiceCliente})", 100),
                Email = email,
                PasswordHash = _passwordHasher.Hash(password),
                Ruolo = RuoloUtente.Cliente,
                ClienteId = cliente.Id,
                Attivo = true
            }, ct);

            generati.Add(new AccountClienteGeneratoDto(cliente.Id, cliente.CodiceCliente, cliente.RagioneSociale, email, password, nota));
        }

        if (generati.Count > 0)
            await _unitOfWork.SaveChangesAsync(ct);

        return generati;
    }

    private static (string Email, string? Nota) ScegliEmail(string? emailCliente, string codiceCliente, HashSet<string> emailUsate)
    {
        var candidata = PrimaEmailValida(emailCliente);

        if (candidata is null)
            return (EmailFittizia(codiceCliente), string.IsNullOrWhiteSpace(emailCliente)
                ? "Email cliente mancante: usato un indirizzo fittizio. Comunicare al cliente il nome utente."
                : $"Email cliente non valida ('{emailCliente}'): usato un indirizzo fittizio.");

        if (emailUsate.Contains(candidata))
            return (EmailFittizia(codiceCliente), $"Email '{candidata}' già usata da un altro account: usato un indirizzo fittizio.");

        return (candidata, null);
    }

    // Nel campo email dei file reali capita di trovare più indirizzi separati da ; , o spazi.
    private static string? PrimaEmailValida(string? testo)
    {
        if (string.IsNullOrWhiteSpace(testo))
            return null;

        foreach (var pezzo in testo.Split(new[] { ';', ',', ' ', '/', '\n' }, StringSplitOptions.RemoveEmptyEntries))
        {
            var email = pezzo.Trim().ToLowerInvariant();
            var chiocciola = email.IndexOf('@');
            if (chiocciola > 0 && chiocciola == email.LastIndexOf('@') && email.IndexOf('.', chiocciola) > chiocciola + 1 && !email.EndsWith('.'))
                return email;
        }

        return null;
    }

    private static string EmailFittizia(string codiceCliente)
    {
        var codice = new string(codiceCliente.ToLowerInvariant().Where(c => char.IsLetterOrDigit(c) || c is '-' or '_').ToArray());
        return $"{(codice.Length == 0 ? "cliente" : codice)}@{DominioFittizio}";
    }

    private static string GeneraPassword()
    {
        while (true)
        {
            var password = new string(Enumerable.Range(0, LunghezzaPassword)
                .Select(_ => Alfabeto[RandomNumberGenerator.GetInt32(Alfabeto.Length)]).ToArray());

            if (password.Any(char.IsUpper) && password.Any(char.IsLower) && password.Any(char.IsDigit))
                return password;
        }
    }

    private static string Tronca(string testo, int massimo) => testo.Length <= massimo ? testo : testo[..massimo];
}
