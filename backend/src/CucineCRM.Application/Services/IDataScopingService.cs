namespace CucineCRM.Application.Services;

/// <summary>
/// Centralizza la regola "chi vede cosa" così che ogni controller/servizio la applichi allo stesso modo,
/// invece di ripetere if sul ruolo sparsi nel codice.
/// - Amministratore / DirettoreCommerciale: nessun filtro (vede tutto).
/// - AreaManager: vede solo gli agenti con AreaManagerId = proprio AgenteId.
/// - Agente: vede solo se stesso.
/// </summary>
public interface IDataScopingService
{
    /// <summary>
    /// Restituisce gli Id degli Agenti visibili dall'utente corrente, oppure null se l'utente
    /// vede tutto (Amministratore/DirettoreCommerciale) e quindi non va applicato alcun filtro.
    /// </summary>
    Task<IReadOnlyList<int>?> GetAgentiVisibiliAsync(CancellationToken ct = default);

    /// <summary>
    /// Per gli account Cliente restituisce l'Id del solo cliente di cui possono vedere ordini e
    /// fatturato; per tutti gli altri ruoli null (nessun vincolo per cliente, vale lo scoping per agente).
    /// </summary>
    Task<int?> GetClienteVincolatoAsync(CancellationToken ct = default);

    /// <summary>True se l'utente corrente può accedere ai dati di uno specifico agente.</summary>
    Task<bool> PuoAccedereAdAgenteAsync(int agenteId, CancellationToken ct = default);
}
