using CucineCRM.Application.Common;
using CucineCRM.Application.DTOs;
using CucineCRM.Application.Interfaces;
using CucineCRM.Domain.Entities;
using CucineCRM.Domain.Enums;

namespace CucineCRM.Application.Services;

public class ComunicazioneService : IComunicazioneService
{
    private readonly IUnitOfWork _unitOfWork;
    private readonly IAsyncQueryExecutor _queryExecutor;
    private readonly ICurrentUserService _currentUser;

    // Solo i formati richiesti (circolari, PDF, Excel): evita che si carichino file arbitrari.
    private static readonly HashSet<string> EstensioniConsentite =
        new(StringComparer.OrdinalIgnoreCase) { ".pdf", ".xlsx", ".xls", ".doc", ".docx" };

    private const long DimensioneMassimaByte = 20 * 1024 * 1024; // 20 MB, coerente con i limiti di import già in uso

    public ComunicazioneService(IUnitOfWork unitOfWork, IAsyncQueryExecutor queryExecutor, ICurrentUserService currentUser)
    {
        _unitOfWork = unitOfWork;
        _queryExecutor = queryExecutor;
        _currentUser = currentUser;
    }

    // Direzione e Visualizzatore vedono tutto; la rete vendita (Agente, AreaManager) vede le comunicazioni
    // per tutti o solo agenti; il Cliente vede quelle per tutti o solo clienti. Ruolo ignoto: solo "Tutti".
    private DestinatariComunicazione[] DestinatariVisibili() => _currentUser.Ruolo switch
    {
        RuoloUtente.Amministratore or RuoloUtente.DirettoreCommerciale or RuoloUtente.Visualizzatore =>
            new[] { DestinatariComunicazione.Tutti, DestinatariComunicazione.SoloAgenti, DestinatariComunicazione.SoloClienti },
        RuoloUtente.Agente or RuoloUtente.AreaManager =>
            new[] { DestinatariComunicazione.Tutti, DestinatariComunicazione.SoloAgenti },
        RuoloUtente.Cliente =>
            new[] { DestinatariComunicazione.Tutti, DestinatariComunicazione.SoloClienti },
        _ => new[] { DestinatariComunicazione.Tutti }
    };

    public async Task<IReadOnlyList<ComunicazioneDto>> GetListaAsync(CancellationToken ct = default)
    {
        // Proiezione esplicita: non scarica mai il contenuto binario del file per il solo elenco.
        var visibili = DestinatariVisibili();
        var righe = await _queryExecutor.ToListAsync(_unitOfWork.Comunicazioni.Query()
            .Where(c => visibili.Contains(c.Destinatari))
            .OrderByDescending(c => c.DataCreazione)
            .Select(c => new
            {
                c.Id, c.Titolo, c.Descrizione, c.NomeFile, c.TipoContenuto, c.DimensioneByte, c.DataCreazione, c.Destinatari,
                NomeCompleto = c.UtentePubblicazione.Nome + " " + c.UtentePubblicazione.Cognome
            }), ct);

        return righe
            .Select(r => new ComunicazioneDto(
                r.Id, r.Titolo, r.Descrizione, r.NomeFile, r.TipoContenuto, r.DimensioneByte, r.DataCreazione, r.NomeCompleto, r.Destinatari))
            .ToList();
    }

    public async Task<ComunicazioneDto> CreaAsync(
        Stream fileStream, string nomeFile, string tipoContenuto, string titolo, string? descrizione,
        DestinatariComunicazione destinatari, int utenteId, CancellationToken ct = default)
    {
        if (string.IsNullOrWhiteSpace(titolo))
            throw new ValidationAppException("Il titolo è obbligatorio.");

        var estensione = Path.GetExtension(nomeFile);
        if (!EstensioniConsentite.Contains(estensione))
            throw new ValidationAppException(
                $"Formato file '{estensione}' non consentito. Sono ammessi: {string.Join(", ", EstensioniConsentite)}.");

        using var memoria = new MemoryStream();
        await fileStream.CopyToAsync(memoria, ct);
        if (memoria.Length == 0)
            throw new ValidationAppException("Il file è vuoto.");
        if (memoria.Length > DimensioneMassimaByte)
            throw new ValidationAppException("Il file supera la dimensione massima consentita di 20 MB.");

        var comunicazione = new Comunicazione
        {
            Titolo = titolo.Trim(),
            Descrizione = string.IsNullOrWhiteSpace(descrizione) ? null : descrizione.Trim(),
            NomeFile = nomeFile,
            TipoContenuto = string.IsNullOrWhiteSpace(tipoContenuto) ? "application/octet-stream" : tipoContenuto,
            DimensioneByte = memoria.Length,
            Contenuto = memoria.ToArray(),
            Destinatari = destinatari,
            UtentePubblicazioneId = utenteId
        };

        await _unitOfWork.Comunicazioni.AddAsync(comunicazione, ct);
        await _unitOfWork.SaveChangesAsync(ct);

        var utente = await _unitOfWork.Utenti.GetByIdAsync(utenteId, ct);
        var nomeCompleto = utente is null ? string.Empty : $"{utente.Nome} {utente.Cognome}";

        return new ComunicazioneDto(
            comunicazione.Id, comunicazione.Titolo, comunicazione.Descrizione, comunicazione.NomeFile,
            comunicazione.TipoContenuto, comunicazione.DimensioneByte, comunicazione.DataCreazione, nomeCompleto, comunicazione.Destinatari);
    }

    public async Task<(byte[] Contenuto, string TipoContenuto, string NomeFile)> ScaricaAsync(int id, CancellationToken ct = default)
    {
        var comunicazione = await _unitOfWork.Comunicazioni.GetByIdAsync(id, ct)
            ?? throw new NotFoundException(nameof(Comunicazione), id);

        if (!DestinatariVisibili().Contains(comunicazione.Destinatari))
            throw new ForbiddenAccessException("Non hai accesso a questa comunicazione.");

        return (comunicazione.Contenuto, comunicazione.TipoContenuto, comunicazione.NomeFile);
    }

    public async Task AggiornaDestinatariAsync(int id, DestinatariComunicazione destinatari, CancellationToken ct = default)
    {
        var comunicazione = await _unitOfWork.Comunicazioni.GetByIdAsync(id, ct)
            ?? throw new NotFoundException(nameof(Comunicazione), id);

        comunicazione.Destinatari = destinatari;
        _unitOfWork.Comunicazioni.Update(comunicazione);
        await _unitOfWork.SaveChangesAsync(ct);
    }

    public async Task EliminaAsync(int id, CancellationToken ct = default)
    {
        var comunicazione = await _unitOfWork.Comunicazioni.GetByIdAsync(id, ct)
            ?? throw new NotFoundException(nameof(Comunicazione), id);

        _unitOfWork.Comunicazioni.SoftDelete(comunicazione);
        await _unitOfWork.SaveChangesAsync(ct);
    }
}
