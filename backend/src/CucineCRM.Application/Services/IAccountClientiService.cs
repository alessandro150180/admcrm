using CucineCRM.Application.DTOs;

namespace CucineCRM.Application.Services;

public interface IAccountClientiService
{
    /// <summary>
    /// Crea un account (ruolo Cliente, sola lettura) per ogni cliente che non ne ha ancora uno.
    /// Il nome utente è l'email del cliente; se manca, non è valida o è già usata, viene assegnato
    /// un indirizzo fittizio basato sul codice cliente, segnalato nel campo Nota. Restituisce solo
    /// gli account appena creati, con la password in chiaro (una sola volta).
    /// </summary>
    Task<IReadOnlyList<AccountClienteGeneratoDto>> GeneraAccountMancantiAsync(CancellationToken ct = default);
}
