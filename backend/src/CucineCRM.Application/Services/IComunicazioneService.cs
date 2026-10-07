using CucineCRM.Application.DTOs;
using CucineCRM.Domain.Enums;

namespace CucineCRM.Application.Services;

public interface IComunicazioneService
{
    /// <summary>Elenco delle comunicazioni visibili al ruolo dell'utente corrente, più recenti per prime.</summary>
    Task<IReadOnlyList<ComunicazioneDto>> GetListaAsync(CancellationToken ct = default);

    /// <summary>Pubblica un nuovo file (circolare/PDF/Excel). Valida estensione e dimensione.</summary>
    Task<ComunicazioneDto> CreaAsync(
        Stream fileStream, string nomeFile, string tipoContenuto, string titolo, string? descrizione,
        DestinatariComunicazione destinatari, int utenteId, CancellationToken ct = default);

    /// <summary>Cambia i destinatari di una comunicazione già pubblicata.</summary>
    Task AggiornaDestinatariAsync(int id, DestinatariComunicazione destinatari, CancellationToken ct = default);

    /// <summary>Contenuto binario di una comunicazione, per il download (solo se visibile al ruolo corrente).</summary>
    Task<(byte[] Contenuto, string TipoContenuto, string NomeFile)> ScaricaAsync(int id, CancellationToken ct = default);

    Task EliminaAsync(int id, CancellationToken ct = default);
}
