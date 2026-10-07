using CucineCRM.Application.DTOs;
using CucineCRM.Application.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace CucineCRM.API.Controllers;

[ApiController]
[Route("api/[controller]")]
[Authorize(Policy = "ConClienti")]
public class FornitoriController : ControllerBase
{
    private readonly IFornitoreService _fornitoreService;

    public FornitoriController(IFornitoreService fornitoreService)
    {
        _fornitoreService = fornitoreService;
    }

    /// <summary>Elenco fornitori (es. Nobilia, NobiSmart, NobiDirect, Comma, GierreDue), usato per
    /// il filtro fatturato per fornitore e per la creazione/import di ordini.</summary>
    [HttpGet]
    public async Task<IActionResult> GetLista(CancellationToken ct)
    {
        var result = await _fornitoreService.GetListaAsync(ct);
        return Ok(result);
    }

    /// <summary>Aggiunge un nuovo fornitore all'elenco. Riservato alla direzione.</summary>
    [HttpPost]
    [Authorize(Policy = "SoloDirezione")]
    public async Task<IActionResult> Crea([FromBody] CreaFornitoreDto request, CancellationToken ct)
    {
        var result = await _fornitoreService.CreaAsync(request, ct);
        return CreatedAtAction(nameof(GetLista), result);
    }
}
