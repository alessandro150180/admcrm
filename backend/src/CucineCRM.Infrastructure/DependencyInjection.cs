using System.Text;
using CucineCRM.Application.Interfaces;
using CucineCRM.Application.Services;
using CucineCRM.Infrastructure.Auth;
using CucineCRM.Infrastructure.GoogleCalendar;
using CucineCRM.Infrastructure.Import;
using CucineCRM.Infrastructure.Persistence;
using CucineCRM.Infrastructure.Repositories;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.IdentityModel.Tokens;

namespace CucineCRM.Infrastructure;

public static class DependencyInjection
{
    public static IServiceCollection AddInfrastructure(this IServiceCollection services, IConfiguration configuration)
    {
        // --- Database ---
        services.AddDbContext<ApplicationDbContext>(options =>
            options.UseNpgsql(
                configuration.GetConnectionString("DefaultConnection"),
                npgsql =>
                {
                    npgsql.MigrationsAssembly(typeof(ApplicationDbContext).Assembly.FullName);
                    // Riprova automaticamente su errori transitori (blip di rete, failover del server):
                    // senza, qualunque interruzione momentanea della connessione a Postgres diventa
                    // un 500 verso il client invece di essere assorbita in modo trasparente.
                    npgsql.EnableRetryOnFailure(maxRetryCount: 3, maxRetryDelay: TimeSpan.FromSeconds(5), errorCodesToAdd: null);
                }));

        // --- Repository / Unit of Work ---
        services.AddScoped<IUnitOfWork, UnitOfWork>();
        services.AddScoped(typeof(IRepository<>), typeof(Repository<>));
        services.AddScoped<IAsyncQueryExecutor, EfAsyncQueryExecutor>();

        // --- Auth ---
        services.Configure<JwtOptions>(configuration.GetSection(JwtOptions.SectionName));
        services.AddSingleton<IPasswordHasher, BCryptPasswordHasher>();
        services.AddScoped<IJwtTokenGenerator, JwtTokenGenerator>();
        services.AddHttpContextAccessor();
        services.AddScoped<ICurrentUserService, CurrentUserService>();

        // --- Application services che dipendono da Infrastructure solo per DI, non per riferimento diretto ---
        services.AddScoped<IAuthService, AuthService>();
        services.AddScoped<IDataScopingService, DataScopingService>();
        services.AddScoped<IClienteService, ClienteService>();
        services.AddScoped<IDashboardService, DashboardService>();
        services.AddScoped<IOrdineService, OrdineService>();
        services.AddScoped<IFornitoreService, FornitoreService>();
        services.AddScoped<IAccountClientiService, AccountClientiService>();
        services.AddScoped<IAttivitaService, AttivitaService>();
        services.AddScoped<INotaClienteService, NotaClienteService>();
        services.AddScoped<IObiettivoVenditaService, ObiettivoVenditaService>();
        services.AddScoped<ISpreadsheetReader, ClosedXmlSpreadsheetReader>();
        services.AddScoped<IImportazioneOrdiniService, ImportazioneOrdiniService>();
        services.AddScoped<IImportazioneClientiService, ImportazioneClientiService>();
        services.AddScoped<IImportazioneFatturatoMensileService, ImportazioneFatturatoMensileService>();
        services.AddScoped<IAuditLogService, AuditLogService>();
        services.AddScoped<INotificaService, NotificaService>();
        services.AddScoped<IComunicazioneService, ComunicazioneService>();

        // --- Google Calendar ---
        services.Configure<GoogleOAuthOptions>(configuration.GetSection(GoogleOAuthOptions.SectionName));
        services.AddHttpClient<IGoogleOAuthClient, GoogleOAuthClient>();
        services.AddScoped<IGoogleCalendarSyncService, GoogleCalendarSyncService>();

        var jwtSection = configuration.GetSection(JwtOptions.SectionName);
        var jwtOptions = jwtSection.Get<JwtOptions>() ?? new JwtOptions();

        services.AddAuthentication(options =>
        {
            options.DefaultAuthenticateScheme = JwtBearerDefaults.AuthenticationScheme;
            options.DefaultChallengeScheme = JwtBearerDefaults.AuthenticationScheme;
        })
        .AddJwtBearer(options =>
        {
            options.TokenValidationParameters = new TokenValidationParameters
            {
                ValidateIssuer = true,
                ValidateAudience = true,
                ValidateLifetime = true,
                ValidateIssuerSigningKey = true,
                ValidIssuer = jwtOptions.Issuer,
                ValidAudience = jwtOptions.Audience,
                IssuerSigningKey = new SymmetricSecurityKey(Encoding.UTF8.GetBytes(jwtOptions.SecretKey)),
                ClockSkew = TimeSpan.FromMinutes(2)
            };
        });

        services.AddAuthorization(options =>
        {
            options.AddPolicy("SoloDirezione", policy =>
                policy.RequireRole("Amministratore", "DirettoreCommerciale"));

            options.AddPolicy("DirezioneOAreaManager", policy =>
                policy.RequireRole("Amministratore", "DirettoreCommerciale", "AreaManager"));

            // Tutti i ruoli autenticati: la restrizione sui dati visibili è applicata da IDataScopingService,
            // non dalla policy di autorizzazione (che qui controlla solo "chi può chiamare l'endpoint").
            // Il Visualizzatore è incluso qui (vede tutto come l'Amministratore) ma non nelle policy
            // sopra: gli endpoint di sola-direzione (creazione agenti/utenti, import, audit log)
            // restano fuori dalla sua portata. Il blocco delle scritture (POST/PUT/DELETE) per questo
            // ruolo è applicato globalmente in Program.cs, non qui.
            options.AddPolicy("TuttiIRuoli", policy =>
                policy.RequireRole("Amministratore", "DirettoreCommerciale", "AreaManager", "Agente", "Visualizzatore"));

            // Come TuttiIRuoli ma include anche gli account Cliente. Usata SOLO sugli endpoint che un
            // cliente può chiamare (proprio fatturato/ordini, elenco fornitori, cambio password).
            // TuttiIRuoli resta volutamente senza Cliente: ogni nuovo endpoint è chiuso ai clienti di default.
            options.AddPolicy("ConClienti", policy =>
                policy.RequireRole("Amministratore", "DirettoreCommerciale", "AreaManager", "Agente", "Visualizzatore", "Cliente"));
        });

        return services;
    }
}
