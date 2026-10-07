using System.Security.Claims;
using CucineCRM.Application.DTOs;
using CucineCRM.Application.Services;
using CucineCRM.Domain.Enums;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace CucineCRM.API.Controllers;

/// <summary>
/// Circolari, PDF e file Excel pubblicati dalla direzione: visibili e scaricabili da tutta la
/// rete vendita e dai clienti, ma la pubblicazione (creazione/eliminazione) resta riservata alla direzione.
/// </summary>
[ApiController]
[Route("api/[controller]")]
[Authorize(Policy = "ConClienti")]
public class ComunicazioniController : ControllerBase
{
    private readonly IComunicazioneService _comunicazioneService;

    public ComunicazioniController(IComunicazioneService comunicazioneService)
    {
        _comunicazioneService = comunicazioneService;
    }

    [HttpGet]
    public async Task<IActionResult> GetLista(CancellationToken ct)
    {
        var result = await _comunicazioneService.GetListaAsync(ct);
        return Ok(result);
    }

    [HttpGet("{id:int}/download")]
    public async Task<IActionResult> Download(int id, CancellationToken ct)
    {
        var (contenuto, tipoContenuto, nomeFile) = await _comunicazioneService.ScaricaAsync(id, ct);
        return File(contenuto, tipoContenuto, nomeFile);
    }

    [HttpPost]
    [Authorize(Policy = "SoloDirezione")]
    [RequestSizeLimit(20_000_000)]
    public async Task<IActionResult> Crea(
        IFormFile file, [FromForm] string titolo, [FromForm] string? descrizione,
        [FromForm] DestinatariComunicazione? destinatari, CancellationToken ct)
    {
        if (file.Length == 0)
            return BadRequest(new { detail = "Il file è vuoto." });

        // Obbligatorio e senza default: dimenticarlo non deve rendere pubblica una circolare riservata.
        if (destinatari is null)
            return BadRequest(new { detail = "Indica a chi è rivolta la comunicazione (destinatari)." });

        var utenteId = int.Parse(User.FindFirstValue(ClaimTypes.NameIdentifier)
            ?? throw new InvalidOperationException("Claim utente mancante."));

        await using var stream = file.OpenReadStream();
        var result = await _comunicazioneService.CreaAsync(stream, file.FileName, file.ContentType, titolo, descrizione, destinatari.Value, utenteId, ct);
        return CreatedAtAction(nameof(GetLista), result);
    }

    [HttpPatch("{id:int}/destinatari")]
    [Authorize(Policy = "SoloDirezione")]
    public async Task<IActionResult> AggiornaDestinatari(int id, [FromBody] AggiornaDestinatariComunicazioneDto request, CancellationToken ct)
    {
        await _comunicazioneService.AggiornaDestinatariAsync(id, request.Destinatari, ct);
        return NoContent();
    }

    [HttpDelete("{id:int}")]
    [Authorize(Policy = "SoloDirezione")]
    public async Task<IActionResult> Elimina(int id, CancellationToken ct)
    {
        await _comunicazioneService.EliminaAsync(id, ct);
        return NoContent();
    }
}
