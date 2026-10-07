using CucineCRM.Application.DTOs;
using CucineCRM.Application.Interfaces;
using CucineCRM.Domain.Entities;
using CucineCRM.Domain.Enums;

namespace CucineCRM.Application.Services;

public class DashboardService : IDashboardService
{
    private readonly IUnitOfWork _unitOfWork;
    private readonly IDataScopingService _scoping;
    private readonly IAsyncQueryExecutor _queryExecutor;
    private readonly ICurrentUserService _currentUser;

    // Quota fissa riservata alla Ditta ADM su ogni fatturato cliente, indipendente dalla
    // percentuale di provvigione (variabile per cliente) riconosciuta all'agente.
    private const decimal PercentualeProvvigioneAdm = 12m;

    public DashboardService(
        IUnitOfWork unitOfWork, IDataScopingService scoping, IAsyncQueryExecutor queryExecutor, ICurrentUserService currentUser)
    {
        _unitOfWork = unitOfWork;
        _scoping = scoping;
        _queryExecutor = queryExecutor;
        _currentUser = currentUser;
    }

    public async Task<DashboardKpiDto> GetKpiPrincipaliAsync(
        IReadOnlyList<int> mesi, int anno, int? agenteId = null, int? clienteId = null,
        IReadOnlyList<int>? fornitoreIds = null, CancellationToken ct = default)
    {
        var ordiniQuery = await GetOrdiniScopedQueryAsync(agenteId, clienteId, fornitoreIds, ct);

        var ordiniMeseCorrente = await _queryExecutor.ToListAsync(ordiniQuery
            .Where(o => mesi.Contains(o.DataOrdine.Month) && o.DataOrdine.Year == anno), ct);

        var ordiniMesePrecedente = await _queryExecutor.ToListAsync(ordiniQuery
            .Where(o => mesi.Contains(o.DataOrdine.Month) && o.DataOrdine.Year == anno - 1), ct);

        var fatturatoCorrente = ordiniMeseCorrente.Sum(o => o.Importo);
        var fatturatoPrecedente = ordiniMesePrecedente.Sum(o => o.Importo);

        var cucineCorrente = ordiniMeseCorrente.Sum(o => o.NumeroCucine);
        var cucinePrecedente = ordiniMesePrecedente.Sum(o => o.NumeroCucine);

        var ordineMedioCorrente = ordiniMeseCorrente.Count == 0 ? 0 : fatturatoCorrente / ordiniMeseCorrente.Count;
        var ordineMedioPrecedente = ordiniMesePrecedente.Count == 0 ? 0 : fatturatoPrecedente / ordiniMesePrecedente.Count;

        // "Nuovi clienti" = clienti la cui DataInserimento cade nel mese/anno richiesto. Non ha
        // senso filtrarlo per fornitore (un cliente non appartiene a un fornitore), quindi qui
        // fornitoreIds non è applicato: resta scoping per ruolo + agente/cliente come prima.
        var clientiQuery = _unitOfWork.Clienti.Query();
        var agentiVisibili = await _scoping.GetAgentiVisibiliAsync(ct);
        if (agentiVisibili is not null)
            clientiQuery = clientiQuery.Where(c => agentiVisibili.Contains(c.AgenteId));
        if (agenteId.HasValue)
            clientiQuery = clientiQuery.Where(c => c.AgenteId == agenteId.Value);
        if (clienteId.HasValue)
            clientiQuery = clientiQuery.Where(c => c.Id == clienteId.Value);

        var nuoviClientiCorrente = await _queryExecutor.CountAsync(
            clientiQuery.Where(c => mesi.Contains(c.DataInserimento.Month) && c.DataInserimento.Year == anno), ct);
        var nuoviClientiPrecedente = await _queryExecutor.CountAsync(
            clientiQuery.Where(c => mesi.Contains(c.DataInserimento.Month) && c.DataInserimento.Year == anno - 1), ct);

        return new DashboardKpiDto(
            FatturatoMensile: CalcolaKpi(fatturatoCorrente, fatturatoPrecedente),
            NuoviClienti: CalcolaKpi(nuoviClientiCorrente, nuoviClientiPrecedente),
            OrdineMedio: CalcolaKpi(ordineMedioCorrente, ordineMedioPrecedente),
            CucineVendute: CalcolaKpi(cucineCorrente, cucinePrecedente)
        );
    }

    public async Task<IReadOnlyList<PuntoGraficoMensileDto>> GetFatturatoMensileAsync(
        int anno, int? agenteId = null, int? clienteId = null,
        IReadOnlyList<int>? fornitoreIds = null, CancellationToken ct = default)
    {
        var ordiniQuery = await GetOrdiniScopedQueryAsync(agenteId, clienteId, fornitoreIds, ct);

        // Include anche i due anni precedenti (quando ci sono dati) per permettere il confronto
        // anno su anno nel grafico, non solo l'anno selezionato.
        var anniConfronto = new[] { anno, anno - 1, anno - 2 };

        // L'aggregazione (GroupBy + Sum) resta lato database; la proiezione al record
        // PuntoGraficoMensileDto viene fatta lato client perché EF Core/Npgsql non riesce a
        // tradurre in SQL un costruttore di record annidato dentro una Select su GroupBy.
        var totaliPerMese = await _queryExecutor.ToListAsync(ordiniQuery
            .Where(o => anniConfronto.Contains(o.DataOrdine.Year))
            .GroupBy(o => new { o.DataOrdine.Year, o.DataOrdine.Month })
            .Select(g => new { g.Key.Year, g.Key.Month, Totale = g.Sum(o => o.Importo) }), ct);

        return totaliPerMese
            .Select(t => new PuntoGraficoMensileDto(t.Month, t.Year, t.Totale))
            .OrderBy(p => p.Anno).ThenBy(p => p.Mese)
            .ToList();
    }

    /// <summary>
    /// Fatturato e provvigione per cliente nei mesi/anno indicati: mostra l'intero portafoglio clienti
    /// dell'agente (anche quelli senza ordini nel periodo, con fatturato 0) oppure un singolo cliente
    /// se <paramref name="clienteId"/> è specificato. Se <paramref name="fornitoreIds"/> è specificato,
    /// il fatturato riportato è ristretto a quei fornitori.
    /// </summary>
    public async Task<IReadOnlyList<ProvvigioneClienteDto>> GetProvvigioniPerClienteAsync(
        IReadOnlyList<int> mesi, int anno, int? agenteId = null, int? clienteId = null,
        IReadOnlyList<int>? fornitoreIds = null, CancellationToken ct = default)
    {
        var agentiVisibili = await _scoping.GetAgentiVisibiliAsync(ct);

        var clientiQuery = _unitOfWork.Clienti.Query();
        if (agentiVisibili is not null)
            clientiQuery = clientiQuery.Where(c => agentiVisibili.Contains(c.AgenteId));
        if (agenteId.HasValue)
            clientiQuery = clientiQuery.Where(c => c.AgenteId == agenteId.Value);
        if (clienteId.HasValue)
            clientiQuery = clientiQuery.Where(c => c.Id == clienteId.Value);

        var clienti = await _queryExecutor.ToListAsync(clientiQuery
            .OrderBy(c => c.RagioneSociale)
            .Select(c => new
            {
                c.Id, c.RagioneSociale, c.AgenteId, AgenteNomeCompleto = c.Agente.Nome + " " + c.Agente.Cognome,
                c.PercentualeProvvigione
            }), ct);

        if (clienti.Count == 0)
            return Array.Empty<ProvvigioneClienteDto>();

        var idClienti = clienti.Select(c => c.Id).ToList();

        // Un'unica query aggregata per il fatturato di tutti i clienti coinvolti nel periodo,
        // invece di interrogare il DB una volta per ogni cliente del portafoglio.
        var fatturatoQuery = _unitOfWork.Ordini.Query()
            .Where(o => mesi.Contains(o.DataOrdine.Month) && o.DataOrdine.Year == anno && idClienti.Contains(o.ClienteId));
        if (fornitoreIds is { Count: > 0 })
            fatturatoQuery = fatturatoQuery.Where(o => fornitoreIds.Contains(o.FornitoreId));

        var fatturatiPerCliente = (await _queryExecutor.ToListAsync(fatturatoQuery
            .GroupBy(o => o.ClienteId)
            .Select(g => new { ClienteId = g.Key, Totale = g.Sum(o => o.Importo) }), ct))
            .ToDictionary(f => f.ClienteId, f => f.Totale);

        // La quota riservata alla Ditta ADM è un dato riservato alla direzione: resta invisibile
        // (azzerato, non solo nascosto lato frontend) per Agente e AreaManager.
        var puoVedereQuotaAdm = _currentUser.Ruolo is RuoloUtente.Amministratore or RuoloUtente.DirettoreCommerciale or RuoloUtente.Visualizzatore;

        return clienti.Select(c =>
        {
            var fatturato = fatturatiPerCliente.GetValueOrDefault(c.Id, 0m);
            var importoProvvigione = Math.Round(fatturato * c.PercentualeProvvigione / 100, 2);

            if (!puoVedereQuotaAdm)
                return new ProvvigioneClienteDto(
                    c.Id, c.RagioneSociale, c.AgenteId, c.AgenteNomeCompleto, fatturato, c.PercentualeProvvigione, importoProvvigione,
                    0, 0, 0);

            var importoProvvigioneAdm = Math.Round(fatturato * PercentualeProvvigioneAdm / 100, 2);
            var differenzaAdmAgente = importoProvvigioneAdm - importoProvvigione;
            return new ProvvigioneClienteDto(
                c.Id, c.RagioneSociale, c.AgenteId, c.AgenteNomeCompleto, fatturato, c.PercentualeProvvigione, importoProvvigione,
                PercentualeProvvigioneAdm, importoProvvigioneAdm, differenzaAdmAgente);
        }).ToList();
    }

    /// <summary>
    /// Restituisce la query sugli Ordini già filtrata secondo lo scope di visibilità dell'utente corrente
    /// (via Cliente.AgenteId), più gli eventuali filtri espliciti per agente/cliente/fornitore.
    /// </summary>
    private async Task<IQueryable<Ordine>> GetOrdiniScopedQueryAsync(
        int? agenteId, int? clienteId, IReadOnlyList<int>? fornitoreIds, CancellationToken ct)
    {
        var agentiVisibili = await _scoping.GetAgentiVisibiliAsync(ct);
        var clienteVincolato = await _scoping.GetClienteVincolatoAsync(ct);

        var query = _unitOfWork.Ordini.Query();

        // Un account Cliente vede solo il proprio fatturato (tutti i fornitori), qualunque filtro passi.
        if (clienteVincolato.HasValue)
            query = query.Where(o => o.ClienteId == clienteVincolato.Value);
        else if (agentiVisibili is not null)
            query = query.Where(o => agentiVisibili.Contains(o.Cliente.AgenteId));

        if (agenteId.HasValue)
            query = query.Where(o => o.Cliente.AgenteId == agenteId.Value);

        if (clienteId.HasValue)
            query = query.Where(o => o.ClienteId == clienteId.Value);

        if (fornitoreIds is { Count: > 0 })
            query = query.Where(o => fornitoreIds.Contains(o.FornitoreId));

        return query;
    }

    private static KpiDto CalcolaKpi(decimal corrente, decimal precedente)
    {
        var differenzaPercentuale = precedente == 0
            ? (corrente == 0 ? 0 : 100)
            : Math.Round((corrente - precedente) / precedente * 100, 1);

        return new KpiDto(corrente, precedente, differenzaPercentuale, differenzaPercentuale >= 0);
    }
}
