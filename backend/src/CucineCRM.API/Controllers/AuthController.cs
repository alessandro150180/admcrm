using System.Security.Claims;
using CucineCRM.Application.DTOs;
using CucineCRM.Application.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace CucineCRM.API.Controllers;

[ApiController]
[Route("api/[controller]")]
public class AuthController : ControllerBase
{
    private readonly IAuthService _authService;

    public AuthController(IAuthService authService)
    {
        _authService = authService;
    }

    /// <summary>Login: restituisce il token JWT da usare come Bearer token nelle chiamate successive.</summary>
    [HttpPost("login")]
    [AllowAnonymous]
    [ProducesResponseType(typeof(LoginResponseDto), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    public async Task<ActionResult<LoginResponseDto>> Login([FromBody] LoginRequestDto request, CancellationToken ct)
    {
        var result = await _authService.LoginAsync(request, ct);
        return Ok(result);
    }

    /// <summary>Crea un nuovo utente. Riservato a Amministratore e Direttore Commerciale.</summary>
    [HttpPost("utenti")]
    [Authorize(Policy = "SoloDirezione")]
    [ProducesResponseType(typeof(UtenteDto), StatusCodes.Status201Created)]
    public async Task<ActionResult<UtenteDto>> CreaUtente([FromBody] CreaUtenteDto request, CancellationToken ct)
    {
        var result = await _authService.CreaUtenteAsync(request, ct);
        return CreatedAtAction(nameof(CreaUtente), new { id = result.Id }, result);
    }

    /// <summary>Cambio password per l'utente attualmente autenticato.</summary>
    [HttpPost("cambia-password")]
    [Authorize(Policy = "ConClienti")]
    public async Task<IActionResult> CambiaPassword([FromBody] CambiaPasswordDto request, CancellationToken ct)
    {
        var utenteId = int.Parse(User.FindFirstValue(ClaimTypes.NameIdentifier)
            ?? throw new InvalidOperationException("Claim utente mancante."));

        await _authService.CambiaPasswordAsync(utenteId, request, ct);
        return NoContent();
    }

    /// <summary>Restituisce i dati dell'utente autenticato, utile al frontend dopo il login/refresh pagina.</summary>
    [HttpGet("me")]
    [Authorize(Policy = "ConClienti")]
    public IActionResult Me()
    {
        return Ok(new
        {
            id = User.FindFirstValue(ClaimTypes.NameIdentifier),
            email = User.FindFirstValue(ClaimTypes.Email),
            nome = User.FindFirstValue(ClaimTypes.Name),
            ruolo = User.FindFirstValue(ClaimTypes.Role),
            agenteId = User.FindFirstValue("agenteId")
        });
    }
}
