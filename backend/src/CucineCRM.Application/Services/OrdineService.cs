using CucineCRM.Application.Common;
using CucineCRM.Application.DTOs;
using CucineCRM.Application.Interfaces;
using CucineCRM.Domain.Entities;

namespace CucineCRM.Application.Services;

public class OrdineService : IOrdineService
{
    private readonly IUnitOfWork _unitOfWork;
    private readonly IDataScopingService _scoping;
    private readonly IAsyncQueryExecutor _queryExecutor;

    public OrdineService(IUnitOfWork unitOfWork, IDataScopingService scoping, IAsyncQueryExecutor queryExecutor)
    {
        _unitOfWork = unitOfWork;
        _scoping = scoping;
        _queryExecutor = queryExecutor;
    }

    public async Task<PagedResult<OrdineDto>> GetListaAsync(FiltriListaDto filtri, CancellationToken ct = default)
    {
        var agentiVisibili = await _scoping.GetAgentiVisibiliAsync(ct);
        var clienteVincolato = await _scoping.GetClienteVincolatoAsync(ct);

        var query = _unitOfWork.Ordini.Query();

        // Scoping per ruolo, sempre applicato prima di qualunque altro filtro (stesso pattern di ClienteService).
        // Un account Cliente vede solo i propri ordini (per tutti i fornitori), non quelli di un agente.
        if (clienteVincolato.HasValue)
            query = query.Where(o => o.ClienteId == clienteVincolato.Value);
        else if (agentiVisibili is not null)
            query = query.Where(o => agentiVisibili.Contains(o.Cliente.AgenteId));

        if (filtri.AgenteId.HasValue)
            query = query.Where(o => o.Cliente.AgenteId == filtri.AgenteId.Value);
        if (filtri.Anno.HasValue)
            query = query.Where(o => o.DataOrdine.Year == filtri.Anno.Value);
        if (filtri.Mese.HasValue)
            query = query.Where(o => o.DataOrdine.Month == filtri.Mese.Value);
        if (filtri.FornitoreIds is { Count: > 0 })
            query = query.Where(o => filtri.FornitoreIds.Contains(o.FornitoreId));

        var totale = await _queryExecutor.CountAsync(query, ct);

        var elementi = await _queryExecutor.ToListAsync(query
            .OrderByDescending(o => o.DataOrdine)
            .Skip((filtri.Pagina - 1) * filtri.Dimensione)
            .Take(filtri.Dimensione)
            .Select(o => new OrdineDto(
                o.Id, o.ClienteId, o.Cliente.RagioneSociale, o.FornitoreId, o.Fornitore.Nome, o.DataOrdine, o.Importo,
                o.NumeroCucine, o.NumeroElettrodomestici, o.NumeroComplementi, o.StatoOrdine,
                o.RiferimentoEsterno)), ct);

        return new PagedResult<OrdineDto>
        {
            Elementi = elementi,
            Pagina = filtri.Pagina,
            Dimensione = filtri.Dimensione,
            TotaleElementi = totale
        };
    }

    public async Task<OrdineDto> GetDettaglioAsync(int ordineId, CancellationToken ct = default)
    {
        var (ordine, cliente) = await CaricaOrdineEClienteAsync(ordineId, ct);

        var clienteVincolato = await _scoping.GetClienteVincolatoAsync(ct);
        var haAccesso = clienteVincolato.HasValue
            ? ordine.ClienteId == clienteVincolato.Value
            : await _scoping.PuoAccedereAdAgenteAsync(cliente.AgenteId, ct);

        if (!haAccesso)
            throw new ForbiddenAccessException("Non hai accesso a questo ordine.");

        return await MapToDtoAsync(ordine, cliente.RagioneSociale, ct);
    }

    public async Task<OrdineDto> CreaAsync(CreaOrdineDto request, CancellationToken ct = default)
    {
        var cliente = await _unitOfWork.Clienti.GetByIdAsync(request.ClienteId, ct)
            ?? throw new NotFoundException(nameof(Cliente), request.ClienteId);

        if (!await _scoping.PuoAccedereAdAgenteAsync(cliente.AgenteId, ct))
            throw new ForbiddenAccessException("Non puoi creare ordini per un cliente che non gestisci.");

        var fornitore = await _unitOfWork.Fornitori.GetByIdAsync(request.FornitoreId, ct)
            ?? throw new NotFoundException(nameof(Fornitore), request.FornitoreId);

        if (!string.IsNullOrWhiteSpace(request.RiferimentoEsterno))
        {
            var duplicato = (await _unitOfWork.Ordini.FindAsync(o => o.RiferimentoEsterno == request.RiferimentoEsterno, ct)).Any();
            if (duplicato)
                throw new ValidationAppException($"Esiste già un ordine con riferimento esterno '{request.RiferimentoEsterno}'.");
        }

        var ordine = new Ordine
        {
            ClienteId = request.ClienteId,
            FornitoreId = request.FornitoreId,
            DataOrdine = request.DataOrdine,
            Importo = request.Importo,
            NumeroCucine = request.NumeroCucine,
            NumeroElettrodomestici = request.NumeroElettrodomestici,
            NumeroComplementi = request.NumeroComplementi,
            RiferimentoEsterno = request.RiferimentoEsterno,
            StatoOrdine = Domain.Enums.StatoOrdine.InAttesa
        };

        await _unitOfWork.Ordini.AddAsync(ordine, ct);
        await _unitOfWork.SaveChangesAsync(ct);

        return MapToDto(ordine, cliente.RagioneSociale, fornitore.Nome);
    }

    public async Task<OrdineDto> AggiornaStatoAsync(int ordineId, AggiornaStatoOrdineDto request, CancellationToken ct = default)
    {
        var (ordine, cliente) = await CaricaOrdineEClienteAsync(ordineId, ct);

        if (!await _scoping.PuoAccedereAdAgenteAsync(cliente.AgenteId, ct))
            throw new ForbiddenAccessException("Non hai accesso a questo ordine.");

        ordine.StatoOrdine = request.NuovoStato;
        _unitOfWork.Ordini.Update(ordine);
        await _unitOfWork.SaveChangesAsync(ct);

        return await MapToDtoAsync(ordine, cliente.RagioneSociale, ct);
    }

    private async Task<(Ordine Ordine, Cliente Cliente)> CaricaOrdineEClienteAsync(int ordineId, CancellationToken ct)
    {
        var ordine = await _unitOfWork.Ordini.GetByIdAsync(ordineId, ct)
            ?? throw new NotFoundException(nameof(Ordine), ordineId);

        var cliente = await _unitOfWork.Clienti.GetByIdAsync(ordine.ClienteId, ct)
            ?? throw new NotFoundException(nameof(Cliente), ordine.ClienteId);

        return (ordine, cliente);
    }

    // Il repository restituisce l'Ordine senza la navigazione Fornitore popolata: va caricato a parte.
    private async Task<OrdineDto> MapToDtoAsync(Ordine o, string clienteRagioneSociale, CancellationToken ct)
    {
        var fornitore = await _unitOfWork.Fornitori.GetByIdAsync(o.FornitoreId, ct);
        return MapToDto(o, clienteRagioneSociale, fornitore?.Nome ?? string.Empty);
    }

    private static OrdineDto MapToDto(Ordine o, string clienteRagioneSociale, string fornitoreNome) => new(
        o.Id, o.ClienteId, clienteRagioneSociale, o.FornitoreId, fornitoreNome, o.DataOrdine, o.Importo,
        o.NumeroCucine, o.NumeroElettrodomestici, o.NumeroComplementi, o.StatoOrdine, o.RiferimentoEsterno);
}
