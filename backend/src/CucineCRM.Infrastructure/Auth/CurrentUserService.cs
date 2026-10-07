using System.Security.Claims;
using CucineCRM.Application.Interfaces;
using CucineCRM.Domain.Enums;
using Microsoft.AspNetCore.Http;

namespace CucineCRM.Infrastructure.Auth;

/// <summary>
/// Espone i dati dell'utente autenticato (letti dai claim del JWT già validato dal middleware
/// di autenticazione di ASP.NET Core) a tutti i servizi Application, senza che questi debbano
/// conoscere HttpContext direttamente.
/// </summary>
public class CurrentUserService : ICurrentUserService
{
    private readonly IHttpContextAccessor _httpContextAccessor;

    public CurrentUserService(IHttpContextAccessor httpContextAccessor)
    {
        _httpContextAccessor = httpContextAccessor;
    }

    private ClaimsPrincipal? User => _httpContextAccessor.HttpContext?.User;

    public bool IsAuthenticated => User?.Identity?.IsAuthenticated ?? false;

    public int? UtenteId
    {
        get
        {
            var value = User?.FindFirst(ClaimTypes.NameIdentifier)?.Value
                ?? User?.FindFirst("sub")?.Value;
            return int.TryParse(value, out var id) ? id : null;
        }
    }

    public RuoloUtente? Ruolo
    {
        get
        {
            var value = User?.FindFirst(ClaimTypes.Role)?.Value;
            return Enum.TryParse<RuoloUtente>(value, out var ruolo) ? ruolo : null;
        }
    }

    public int? ClienteId
    {
        get
        {
            var value = User?.FindFirst("clienteId")?.Value;
            return int.TryParse(value, out var id) ? id : null;
        }
    }

    public int? AgenteId
    {
        get
        {
            var value = User?.FindFirst("agenteId")?.Value;
            return int.TryParse(value, out var id) ? id : null;
        }
    }
}
