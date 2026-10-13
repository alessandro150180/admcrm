using System.Globalization;
using System.Text.Json;
using CucineCRM.Application.Common;
using CucineCRM.Application.DTOs;
using CucineCRM.Application.Interfaces;
using CucineCRM.Domain.Entities;
using CucineCRM.Domain.Enums;

namespace CucineCRM.Application.Services;

public class ImportazioneFatturatoMensileService : IImportazioneFatturatoMensileService
{
    private static readonly Dictionary<string, int> MesiItaliani = new(StringComparer.OrdinalIgnoreCase)
    {
        ["gennaio"] = 1, ["febbraio"] = 2, ["marzo"] = 3, ["aprile"] = 4, ["maggio"] = 5, ["giugno"] = 6,
        ["luglio"] = 7, ["agosto"] = 8, ["settembre"] = 9, ["ottobre"] = 10, ["novembre"] = 11, ["dicembre"] = 12,
    };

    private readonly IUnitOfWork _unitOfWork;
    private readonly IAsyncQueryExecutor _queryExecutor;
    private readonly ISpreadsheetReader _spreadsheetReader;
    private readonly ICurrentUserService _currentUser;

    public ImportazioneFatturatoMensileService(
        IUnitOfWork unitOfWork, IAsyncQueryExecutor queryExecutor, ISpreadsheetReader spreadsheetReader, ICurrentUserService currentUser)
    {
        _unitOfWork = unitOfWork;
        _queryExecutor = queryExecutor;
        _spreadsheetReader = spreadsheetReader;
        _currentUser = currentUser;
    }

    public async Task<ImportazioneRisultatoDto> ImportaFatturatoMensileAsync(Stream file, string nomeFile, int fornitoreId, CancellationToken ct = default)
    {
        var utenteId = _currentUser.UtenteId
            ?? throw new AuthenticationException("Utente non autenticato.");

        if (await _unitOfWork.Fornitori.GetByIdAsync(fornitoreId, ct) is null)
            throw new ValidationAppException($"Nessun fornitore con id {fornitoreId}.");

        var righe = _spreadsheetReader.LeggiRighe(file);

        var colonneMese = righe.Count == 0
            ? new List<(string Chiave, int Anno, int Mese)>()
            : righe[0].Keys
                .Select(chiave => (Chiave: chiave, Periodo: ProvaEstraiMeseAnno(chiave)))
                .Where(x => x.Periodo is not null)
                .Select(x => (x.Chiave, x.Periodo!.Value.Anno, x.Periodo!.Value.Mese))
                .ToList();

        var clientiPerCodice = (await _queryExecutor.ToListAsync(_unitOfWork.Clienti.Query()
            .Select(c => new { c.Id, c.CodiceCliente }), ct))
            .GroupBy(c => c.CodiceCliente, StringComparer.OrdinalIgnoreCase)
            .ToDictionary(g => g.Key, g => g.First().Id, StringComparer.OrdinalIgnoreCase);

        // Gli ordini importati prima dell'introduzione dei fornitori hanno il riferimento di vecchio formato
        // ("FATT-{cliente}-{anno}{mese}", senza fornitore). Per il confronto li si porta al formato attuale
        // usando il fornitore che l'ordine ha già in database: così un nuovo import dello stesso file non li
        // ricrea come ordini nuovi (raddoppiando il fatturato).
        var riferimentiEsistenti = new HashSet<string>(
            (await _queryExecutor.ToListAsync(_unitOfWork.Ordini.Query()
                .Where(o => o.RiferimentoEsterno != null)
                .Select(o => new { Riferimento = o.RiferimentoEsterno!, o.FornitoreId }), ct))
                .Select(o => NormalizzaRiferimento(o.Riferimento, o.FornitoreId)),
            StringComparer.OrdinalIgnoreCase);

        var log = new List<RigaImportLogDto>();
        var nuoviOrdini = new List<Ordine>();
        var clientiDaAggiornareProvvigione = new List<(int ClienteId, decimal Percentuale)>();
        var scartate = 0;
        var duplicateCelle = 0;

        for (var i = 0; i < righe.Count; i++)
        {
            var numeroRiga = i + 2; // +1 per header, +1 perché l'utente conta le righe da 1
            var riga = righe[i];

            var codiceCliente = ValoreCella(riga, "CodiceCliente");
            if (string.IsNullOrWhiteSpace(codiceCliente))
            {
                log.Add(new RigaImportLogDto(numeroRiga, "Scartata", "Codice cliente mancante."));
                scartate++;
                continue;
            }

            if (!clientiPerCodice.TryGetValue(codiceCliente, out var clienteId))
            {
                log.Add(new RigaImportLogDto(numeroRiga, "Scartata", $"Nessun cliente con codice '{codiceCliente}'."));
                scartate++;
                continue;
            }

            var provvigioneTesto = ValoreCella(riga, "Provvigione", "PercentualeProvvigione");
            if (provvigioneTesto is not null)
                clientiDaAggiornareProvvigione.Add((clienteId, ParsaPercentualeProvvigione(provvigioneTesto)));

            var meseImportati = 0;
            var meseDuplicati = 0;
            foreach (var (chiave, anno, mese) in colonneMese)
            {
                if (!riga.TryGetValue(chiave, out var testoValore) || string.IsNullOrWhiteSpace(testoValore))
                    continue;

                if (!TryParseImporto(testoValore, out var importo) || importo == 0)
                    continue; // nessun fatturato quel mese: non è un errore, semplicemente non c'è nulla da importare

                // Include il fornitore nel riferimento: lo stesso cliente/mese può avere fatturato
                // separato per più fornitori (Nobilia, NobiSmart, ...), senza scontrarsi sull'univocità.
                var riferimento = CostruisciRiferimento(fornitoreId, codiceCliente, anno, mese);
                if (riferimentiEsistenti.Contains(riferimento))
                {
                    meseDuplicati++;
                    continue;
                }

                nuoviOrdini.Add(new Ordine
                {
                    ClienteId = clienteId,
                    FornitoreId = fornitoreId,
                    DataOrdine = new DateTime(anno, mese, 1, 0, 0, 0, DateTimeKind.Utc),
                    Importo = importo,
                    StatoOrdine = StatoOrdine.Consegnato, // fatturato storico già realizzato, non un ordine in corso
                    RiferimentoEsterno = riferimento
                });
                riferimentiEsistenti.Add(riferimento);
                meseImportati++;
            }

            duplicateCelle += meseDuplicati;

            if (meseImportati == 0 && meseDuplicati == 0)
            {
                log.Add(new RigaImportLogDto(numeroRiga, "Scartata", "Nessun mese con fatturato valorizzato in questa riga."));
                scartate++;
            }
            else if (meseImportati == 0)
            {
                log.Add(new RigaImportLogDto(numeroRiga, "Duplicata", $"Tutti i {meseDuplicati} mesi valorizzati erano già stati importati."));
            }
            else
            {
                log.Add(new RigaImportLogDto(numeroRiga, "Importata",
                    $"{meseImportati} mesi importati" + (meseDuplicati > 0 ? $", {meseDuplicati} già presenti" : "")));
            }
        }

        // Il campo PeriodoCompetenza è pensato per un singolo mese ("2026-06", max 7 caratteri): un
        // file a pivot ne copre diversi, quindi si usa qui il primo mese trovato come riferimento.
        string periodoCompetenza;
        if (colonneMese.Count == 0)
        {
            periodoCompetenza = "n/d";
        }
        else
        {
            var primoPeriodo = colonneMese.OrderBy(c => c.Anno * 100 + c.Mese).First();
            periodoCompetenza = $"{primoPeriodo.Anno:D4}-{primoPeriodo.Mese:D2}";
        }

        var importazione = new Importazione
        {
            NomeFile = nomeFile,
            DataImportazione = DateTime.UtcNow,
            UtenteImportazioneId = utenteId,
            PeriodoCompetenza = periodoCompetenza,
            RighePlesse = righe.Count,
            RigheImportate = nuoviOrdini.Count,
            RigheScartate = scartate,
            RigheDuplicate = duplicateCelle,
            LogEsito = JsonSerializer.Serialize(log),
            Completata = true
        };

        await _unitOfWork.Importazioni.AddAsync(importazione, ct);
        await _unitOfWork.SaveChangesAsync(ct); // necessario per ottenere l'Id generato di Importazione

        foreach (var ordine in nuoviOrdini)
        {
            ordine.ImportazioneId = importazione.Id;
            await _unitOfWork.Ordini.AddAsync(ordine, ct);
        }

        foreach (var (clienteId, percentuale) in clientiDaAggiornareProvvigione)
        {
            var cliente = await _unitOfWork.Clienti.GetByIdAsync(clienteId, ct);
            if (cliente is not null)
            {
                cliente.PercentualeProvvigione = percentuale;
                _unitOfWork.Clienti.Update(cliente);
            }
        }

        await _unitOfWork.SaveChangesAsync(ct);

        return new ImportazioneRisultatoDto(
            importazione.Id, importazione.NomeFile, importazione.DataImportazione, importazione.PeriodoCompetenza,
            importazione.RighePlesse, importazione.RigheImportate, importazione.RigheScartate, importazione.RigheDuplicate,
            importazione.Completata, importazione.LogEsito);
    }

    public async Task<int> EliminaFatturatoMensileAsync(int anno, int mese, int fornitoreId, CancellationToken ct = default)
    {
        // Il prefisso "FATT-" e il suffisso "-{anno}{mese}" riconoscono gli ordini sintetici di questo import,
        // e il fornitore è quello salvato sull'ordine: vale sia per i riferimenti nuovi ("FATT-F1-...") sia per
        // quelli di vecchio formato, e non tocca ordini manuali o di altri fornitori nello stesso mese.
        var suffisso = $"-{anno}{mese:D2}";

        var daEliminare = await _queryExecutor.ToListAsync(_unitOfWork.Ordini.Query()
            .Where(o => o.RiferimentoEsterno != null
                && o.FornitoreId == fornitoreId
                && o.RiferimentoEsterno.StartsWith("FATT-")
                && o.RiferimentoEsterno.EndsWith(suffisso)), ct);

        foreach (var ordine in daEliminare)
            _unitOfWork.Ordini.SoftDelete(ordine);

        if (daEliminare.Count > 0)
            await _unitOfWork.SaveChangesAsync(ct);

        return daEliminare.Count;
    }

    private static string CostruisciRiferimento(int fornitoreId, string codiceCliente, int anno, int mese) =>
        $"FATT-F{fornitoreId}-{codiceCliente}-{anno}{mese:D2}";

    /// <summary>Porta un riferimento di vecchio formato ("FATT-{cliente}-{anno}{mese}") al formato attuale, usando il
    /// fornitore dell'ordine. Quelli già nel formato attuale restano invariati.</summary>
    private static string NormalizzaRiferimento(string riferimento, int fornitoreId)
    {
        var prefissoAttuale = $"FATT-F{fornitoreId}-";
        if (riferimento.StartsWith(prefissoAttuale, StringComparison.OrdinalIgnoreCase) || !riferimento.StartsWith("FATT-", StringComparison.OrdinalIgnoreCase))
            return riferimento;

        return prefissoAttuale + riferimento["FATT-".Length..];
    }

    private static (int Anno, int Mese)? ProvaEstraiMeseAnno(string intestazione)
    {
        var parti = intestazione.Trim().Split(' ', StringSplitOptions.RemoveEmptyEntries);
        if (parti.Length != 2) return null;
        if (!MesiItaliani.TryGetValue(parti[0], out var mese)) return null;
        if (!int.TryParse(parti[1], NumberStyles.Integer, CultureInfo.InvariantCulture, out var anno)) return null;
        return (anno, mese);
    }

    private static string? ValoreCella(IReadOnlyDictionary<string, string> riga, params string[] alias)
    {
        foreach (var nome in alias)
        {
            if (riga.TryGetValue(nome, out var valore) && !string.IsNullOrWhiteSpace(valore))
                return valore.Trim();
        }
        return null;
    }

    // Le celle numeriche arrivano dal lettore con il punto decimale ("25003.45"); un testo con la virgola
    // ("25003,45", cella salvata come testo) è invece scritto all'italiana. Con la sola cultura invariante la
    // virgola verrebbe letta come separatore delle migliaia e l'importo risulterebbe cento volte più grande.
    private static bool TryParseImporto(string testo, out decimal importo) =>
        testo.Contains(',')
            ? decimal.TryParse(testo, NumberStyles.Number, CultureInfo.GetCultureInfo("it-IT"), out importo)
            : decimal.TryParse(testo, NumberStyles.Number, CultureInfo.InvariantCulture, out importo);

    /// <summary>Come per l'import anagrafiche: un valore &lt;=1 è trattato come frazione (0,07 = 7%).</summary>
    private static decimal ParsaPercentualeProvvigione(string testo)
    {
        if (!TryParseImporto(testo, out var valore))
            return 0m;
        return valore > 0 && valore <= 1 ? valore * 100 : valore;
    }
}
