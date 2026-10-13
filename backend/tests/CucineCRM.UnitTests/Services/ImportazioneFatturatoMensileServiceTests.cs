using CucineCRM.Application.DTOs;
using CucineCRM.Application.Interfaces;
using CucineCRM.Application.Services;
using CucineCRM.Domain.Entities;
using CucineCRM.Domain.Enums;
using FluentAssertions;
using Moq;
using Xunit;

namespace CucineCRM.UnitTests.Services;

/// <summary>Esegue le query direttamente in memoria, al posto di EF Core.</summary>
internal sealed class InMemoryQueryExecutor : IAsyncQueryExecutor
{
    public Task<int> CountAsync<TSource>(IQueryable<TSource> query, CancellationToken ct = default) => Task.FromResult(query.Count());
    public Task<List<TSource>> ToListAsync<TSource>(IQueryable<TSource> query, CancellationToken ct = default) => Task.FromResult(query.ToList());
    public Task<decimal> SumAsync(IQueryable<decimal> query, CancellationToken ct = default) => Task.FromResult(query.Sum());
}

public class ImportazioneFatturatoMensileServiceTests
{
    private const int Nobilia = 1;
    private const int NobiSmart = 2;

    private readonly List<Ordine> _ordiniInDb = new();
    private readonly List<Ordine> _ordiniAggiunti = new();
    private readonly Mock<IUnitOfWork> _unitOfWork = new();
    private readonly Mock<IRepository<Cliente>> _clientiRepo = new();
    private readonly Mock<IRepository<Ordine>> _ordiniRepo = new();
    private readonly Mock<IRepository<Fornitore>> _fornitoriRepo = new();
    private readonly Mock<IRepository<Importazione>> _importazioniRepo = new();
    private readonly Mock<ISpreadsheetReader> _reader = new();
    private readonly Mock<ICurrentUserService> _currentUser = new();
    private readonly InMemoryQueryExecutor _executor = new();
    private readonly ImportazioneFatturatoMensileService _sut;

    public ImportazioneFatturatoMensileServiceTests()
    {
        _unitOfWork.Setup(u => u.Clienti).Returns(_clientiRepo.Object);
        _unitOfWork.Setup(u => u.Ordini).Returns(_ordiniRepo.Object);
        _unitOfWork.Setup(u => u.Fornitori).Returns(_fornitoriRepo.Object);
        _unitOfWork.Setup(u => u.Importazioni).Returns(_importazioniRepo.Object);

        _clientiRepo.Setup(r => r.Query()).Returns(new[] { new Cliente { Id = 10, CodiceCliente = "26011" } }.AsQueryable());
        _clientiRepo.Setup(r => r.GetByIdAsync(It.IsAny<int>(), default)).ReturnsAsync((Cliente?)null);
        _ordiniRepo.Setup(r => r.Query()).Returns(() => _ordiniInDb.AsQueryable());
        _ordiniRepo.Setup(r => r.AddAsync(It.IsAny<Ordine>(), default)).Callback<Ordine, CancellationToken>((o, _) => _ordiniAggiunti.Add(o)).Returns(Task.CompletedTask);
        _fornitoriRepo.Setup(r => r.GetByIdAsync(It.IsAny<int>(), default)).ReturnsAsync(new Fornitore { Id = 1, Nome = "x" });
        _currentUser.Setup(c => c.UtenteId).Returns(1);
        _sut = new ImportazioneFatturatoMensileService(_unitOfWork.Object, _executor, _reader.Object, _currentUser.Object);
    }

    private void FileConRiga(string mese, string importo) =>
        _reader.Setup(r => r.LeggiRighe(It.IsAny<Stream>())).Returns(new[]
        {
            (IReadOnlyDictionary<string, string>)new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase)
            {
                ["CodiceCliente"] = "26011", [mese] = importo
            }
        });

    private Task<ImportazioneRisultatoDto> Importa(int fornitoreId) =>
        _sut.ImportaFatturatoMensileAsync(new MemoryStream(new byte[] { 1 }), "f.xlsx", fornitoreId);

    [Fact]
    public async Task Importa_OrdineEsistenteConRiferimentoVecchioFormato_NonLoRicrea()
    {
        // Ordine importato prima dei fornitori: riferimento senza "F{id}", fornitore assegnato dal backfill
        _ordiniInDb.Add(new Ordine { Id = 1, ClienteId = 10, FornitoreId = Nobilia, RiferimentoEsterno = "FATT-26011-202604" });
        FileConRiga("Aprile 2026", "28118");

        var esito = await Importa(Nobilia);

        _ordiniAggiunti.Should().BeEmpty("il fatturato di aprile per Nobilia c'è già");
        esito.RigheImportate.Should().Be(0);
        esito.RigheDuplicate.Should().Be(1);
    }

    [Fact]
    public async Task Importa_OrdineEsistenteNelFormatoAttuale_NonLoRicrea()
    {
        _ordiniInDb.Add(new Ordine { Id = 1, ClienteId = 10, FornitoreId = Nobilia, RiferimentoEsterno = "FATT-F1-26011-202604" });
        FileConRiga("Aprile 2026", "28118");

        var esito = await Importa(Nobilia);

        _ordiniAggiunti.Should().BeEmpty();
        esito.RigheDuplicate.Should().Be(1);
    }

    [Fact]
    public async Task Importa_StessoMeseMaAltroFornitore_CreaIlNuovoOrdine()
    {
        // Il riferimento vecchio appartiene a Nobilia: lo stesso mese per NobiSmart è fatturato diverso
        _ordiniInDb.Add(new Ordine { Id = 1, ClienteId = 10, FornitoreId = Nobilia, RiferimentoEsterno = "FATT-26011-202604" });
        FileConRiga("Aprile 2026", "5000");

        var esito = await Importa(NobiSmart);

        _ordiniAggiunti.Should().ContainSingle();
        _ordiniAggiunti[0].FornitoreId.Should().Be(NobiSmart);
        _ordiniAggiunti[0].RiferimentoEsterno.Should().Be("FATT-F2-26011-202604");
        esito.RigheImportate.Should().Be(1);
    }

    // "25003.45" è ciò che il lettore restituisce per una cella numerica; "25003,45" per una cella di testo italiana
    [Theory]
    [InlineData("25003.45")]
    [InlineData("25003,45")]
    [InlineData("25.003,45")]
    public async Task Importa_MeseNuovo_LoCreaConIlFornitoreIndicato(string importoNelFile)
    {
        _ordiniInDb.Add(new Ordine { Id = 1, ClienteId = 10, FornitoreId = Nobilia, RiferimentoEsterno = "FATT-26011-202604" });
        FileConRiga("Settembre 2026", importoNelFile);

        var esito = await Importa(Nobilia);

        _ordiniAggiunti.Should().ContainSingle();
        _ordiniAggiunti[0].Importo.Should().Be(25003.45m);
        _ordiniAggiunti[0].FornitoreId.Should().Be(Nobilia);
        _ordiniAggiunti[0].RiferimentoEsterno.Should().Be("FATT-F1-26011-202609");
        esito.RigheImportate.Should().Be(1);
    }

    [Fact]
    public async Task Elimina_CancellaSoloIlFornitoreIndicatoEIlMeseGiusto_ancheConRiferimentoVecchio()
    {
        var vecchioNobilia = new Ordine { Id = 1, ClienteId = 10, FornitoreId = Nobilia, RiferimentoEsterno = "FATT-26011-202608" };
        var nuovoNobilia = new Ordine { Id = 2, ClienteId = 10, FornitoreId = Nobilia, RiferimentoEsterno = "FATT-F1-26011-202608" };
        var altroFornitore = new Ordine { Id = 3, ClienteId = 10, FornitoreId = NobiSmart, RiferimentoEsterno = "FATT-F2-26011-202608" };
        var altroMese = new Ordine { Id = 4, ClienteId = 10, FornitoreId = Nobilia, RiferimentoEsterno = "FATT-26011-202607" };
        var manuale = new Ordine { Id = 5, ClienteId = 10, FornitoreId = Nobilia, RiferimentoEsterno = "ORD-123-202608" };
        _ordiniInDb.AddRange(new[] { vecchioNobilia, nuovoNobilia, altroFornitore, altroMese, manuale });

        var eliminati = await _sut.EliminaFatturatoMensileAsync(2026, 8, Nobilia);

        eliminati.Should().Be(2);
        _ordiniRepo.Verify(r => r.SoftDelete(vecchioNobilia), Times.Once);
        _ordiniRepo.Verify(r => r.SoftDelete(nuovoNobilia), Times.Once);
        _ordiniRepo.Verify(r => r.SoftDelete(altroFornitore), Times.Never);
        _ordiniRepo.Verify(r => r.SoftDelete(altroMese), Times.Never);
        _ordiniRepo.Verify(r => r.SoftDelete(manuale), Times.Never);
    }
}
