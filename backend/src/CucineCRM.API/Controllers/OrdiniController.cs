using CucineCRM.API.Export;
using CucineCRM.Application.DTOs;
using CucineCRM.Application.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace CucineCRM.API.Controllers;

[ApiController]
[Route("api/[controller]")]
[Authorize(Policy = "ConClienti")]
public class OrdiniController : ControllerBase
{
    private readonly IOrdineService _ordineService;

    public OrdiniController(IOrdineService ordineService)
    {
        _ordineService = ordineService;
    }

    /// <summary>Elenco ordini paginato e filtrabile (agente, anno, mese). Già ristretto allo scope dell'utente.</summary>
    [HttpGet]
    public async Task<IActionResult> GetLista([FromQuery] FiltriListaDto filtri, CancellationToken ct)
    {
        var result = await _ordineService.GetListaAsync(filtri, ct);
        return Ok(result);
    }

    [HttpGet("{id:int}")]
    public async Task<IActionResult> GetDettaglio(int id, CancellationToken ct)
    {
        var result = await _ordineService.GetDettaglioAsync(id, ct);
        return Ok(result);
    }

    [HttpPost]
    [Authorize(Policy = "TuttiIRuoli")]
    public async Task<IActionResult> Crea([FromBody] CreaOrdineDto request, CancellationToken ct)
    {
        var result = await _ordineService.CreaAsync(request, ct);
        return CreatedAtAction(nameof(GetDettaglio), new { id = result.Id }, result);
    }

    /// <summary>Aggiorna lo stato dell'ordine (es. Confermato, InProduzione, Spedito, Consegnato, Annullato).</summary>
    [HttpPatch("{id:int}/stato")]
    [Authorize(Policy = "TuttiIRuoli")]
    public async Task<IActionResult> AggiornaStato(int id, [FromBody] AggiornaStatoOrdineDto request, CancellationToken ct)
    {
        var result = await _ordineService.AggiornaStatoAsync(id, request, ct);
        return Ok(result);
    }

    /// <summary>Esporta in CSV l'elenco ordini (stessi filtri della lista, senza paginazione).</summary>
    [HttpGet("export/csv")]
    public async Task<IActionResult> EsportaCsv([FromQuery] FiltriListaDto filtri, CancellationToken ct)
    {
        var tutti = await _ordineService.GetListaAsync(filtri with { Pagina = 1, Dimensione = 100_000 }, ct);
        var csv = CsvExporter.EsportaOrdini(tutti.Elementi);
        return File(csv, "text/csv", "ordini.csv");
    }
}
