using CucineCRM.Application.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace CucineCRM.API.Controllers;

[ApiController]
[Route("api/[controller]")]
[Authorize(Policy = "ConClienti")]
public class DashboardController : ControllerBase
{
    private readonly IDashboardService _dashboardService;

    public DashboardController(IDashboardService dashboardService)
    {
        _dashboardService = dashboardService;
    }

    /// <summary>I 4 KPI della Home (fatturato mensile, nuovi clienti, ordine medio, cucine vendute).
    /// Il parametro mesi accetta uno o più valori (?mesi=6&amp;mesi=7&amp;mesi=8): con più mesi i
    /// valori sono la somma sull'insieme selezionato.</summary>
    [HttpGet("kpi")]
    public async Task<IActionResult> GetKpi(
        [FromQuery] int[] mesi, [FromQuery] int anno, [FromQuery] int? agenteId, [FromQuery] int? clienteId,
        [FromQuery] int[]? fornitoreIds, CancellationToken ct)
    {
        var result = await _dashboardService.GetKpiPrincipaliAsync(mesi, anno, agenteId, clienteId, fornitoreIds, ct);
        return Ok(result);
    }

    /// <summary>Serie mensile del fatturato per il grafico a colonne, anno indicato + i due precedenti.</summary>
    [HttpGet("fatturato-mensile")]
    public async Task<IActionResult> GetFatturatoMensile(
        [FromQuery] int anno, [FromQuery] int? agenteId, [FromQuery] int? clienteId,
        [FromQuery] int[]? fornitoreIds, CancellationToken ct)
    {
        var result = await _dashboardService.GetFatturatoMensileAsync(anno, agenteId, clienteId, fornitoreIds, ct);
        return Ok(result);
    }

    /// <summary>Fatturato e provvigione per cliente: portafoglio di un agente, o singolo cliente se clienteId è specificato.</summary>
    [HttpGet("provvigioni")]
    [Authorize(Policy = "TuttiIRuoli")] // le provvigioni non sono visibili ai clienti
    public async Task<IActionResult> GetProvvigioni(
        [FromQuery] int[] mesi, [FromQuery] int anno, [FromQuery] int? agenteId, [FromQuery] int? clienteId,
        [FromQuery] int[]? fornitoreIds, CancellationToken ct)
    {
        var result = await _dashboardService.GetProvvigioniPerClienteAsync(mesi, anno, agenteId, clienteId, fornitoreIds, ct);
        return Ok(result);
    }
}
