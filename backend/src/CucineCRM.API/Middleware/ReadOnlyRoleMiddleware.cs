using System.Text.Json;

// Contains(string, IEqualityComparer<string>) è un metodo di estensione LINQ.
using System.Linq;

namespace CucineCRM.API.Middleware;

/// <summary>
/// Blocca globalmente qualsiasi richiesta di scrittura (tutto tranne GET/HEAD/OPTIONS) per
/// gli utenti con ruolo Visualizzatore, indipendentemente dall'endpoint chiamato. Applicato qui
/// e non per singolo controller/azione per garantire che nessuna rotta, presente o futura,
/// possa essere dimenticata.
/// </summary>
public class ReadOnlyRoleMiddleware
{
    private readonly RequestDelegate _next;

    public ReadOnlyRoleMiddleware(RequestDelegate next)
    {
        _next = next;
    }

    // Il cambio password riguarda il proprio account, non i dati del CRM: resta permesso
    // anche per il ruolo di sola lettura.
    private static readonly string[] PercorsiConsentiti = { "/api/auth/cambia-password" };

    public async Task InvokeAsync(HttpContext context)
    {
        var isScrittura = !HttpMethods.IsGet(context.Request.Method)
            && !HttpMethods.IsHead(context.Request.Method)
            && !HttpMethods.IsOptions(context.Request.Method);

        var isPercorsoConsentito = PercorsiConsentiti.Contains(context.Request.Path.Value, StringComparer.OrdinalIgnoreCase);

        if (isScrittura && !isPercorsoConsentito && (context.User.IsInRole("Visualizzatore") || context.User.IsInRole("Cliente")))
        {
            context.Response.StatusCode = StatusCodes.Status403Forbidden;
            context.Response.ContentType = "application/json";
            await context.Response.WriteAsync(JsonSerializer.Serialize(new
            {
                title = "Accesso negato",
                status = StatusCodes.Status403Forbidden,
                detail = "Questo account ha accesso di sola lettura: nessuna modifica è consentita."
            }));
            return;
        }

        await _next(context);
    }
}
