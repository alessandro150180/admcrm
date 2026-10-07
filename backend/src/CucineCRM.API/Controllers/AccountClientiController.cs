using CucineCRM.Application.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace CucineCRM.API.Controllers;

[ApiController]
[Route("api/account-clienti")]
[Authorize(Policy = "SoloDirezione")]
public class AccountClientiController : ControllerBase
{
    private readonly IAccountClientiService _accountClientiService;

    public AccountClientiController(IAccountClientiService accountClientiService)
    {
        _accountClientiService = accountClientiService;
    }

    /// <summary>Crea un account di sola lettura per ogni cliente che non ne ha uno e restituisce le
    /// credenziali appena generate (la password in chiaro compare solo in questa risposta).</summary>
    [HttpPost("genera")]
    public async Task<IActionResult> Genera(CancellationToken ct)
    {
        var result = await _accountClientiService.GeneraAccountMancantiAsync(ct);
        return Ok(result);
    }
}
