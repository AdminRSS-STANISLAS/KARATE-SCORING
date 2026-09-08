using FkcScoring.Core.Data;
using FkcScoring.Core.Domain;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using Xunit;

namespace FkcScoring.Core.Tests;

public class SecuriteServiceTests : IDisposable
{
    private readonly SqliteConnection _connection;
    private readonly FkcScoringContext _db;

    public SecuriteServiceTests()
    {
        _connection = new SqliteConnection("DataSource=:memory:");
        _connection.Open();
        var options = new DbContextOptionsBuilder<FkcScoringContext>().UseSqlite(_connection).Options;
        _db = new FkcScoringContext(options);
        _db.Database.EnsureCreated();
    }

    public void Dispose()
    {
        _db.Dispose();
        _connection.Dispose();
    }

    [Fact]
    public async Task AucunCodeConfigure_EstConfigureFaux_EtVerifierCodeToujoursVrai()
    {
        // Pas de régression sur le comportement avant la Phase 7 : tant qu'aucun code n'a jamais été
        // défini, l'action protégée (reset complet) reste autorisée quel que soit le code fourni.
        Assert.False(await SecuriteService.EstConfigureAsync(_db));
        Assert.True(await SecuriteService.VerifierCodeAsync(_db, null));
        Assert.True(await SecuriteService.VerifierCodeAsync(_db, "n'importe quoi"));
    }

    [Fact]
    public async Task DefinirCode_PremiereFois_NeRequiertPasAncienCode()
    {
        var ok = await SecuriteService.DefinirCodeAsync(_db, "secret123", ancienCode: null);

        Assert.True(ok);
        Assert.True(await SecuriteService.EstConfigureAsync(_db));
    }

    [Fact]
    public async Task VerifierCode_BonCode_Vrai_MauvaisCode_Faux()
    {
        await SecuriteService.DefinirCodeAsync(_db, "secret123", null);

        Assert.True(await SecuriteService.VerifierCodeAsync(_db, "secret123"));
        Assert.False(await SecuriteService.VerifierCodeAsync(_db, "mauvais"));
        Assert.False(await SecuriteService.VerifierCodeAsync(_db, ""));
        Assert.False(await SecuriteService.VerifierCodeAsync(_db, null));
    }

    [Fact]
    public async Task DefinirCode_ChangementSansAncienCodeCorrect_Echoue_EtAncienCodeResteActif()
    {
        await SecuriteService.DefinirCodeAsync(_db, "premier", null);

        var ok = await SecuriteService.DefinirCodeAsync(_db, "second", ancienCode: "faux");

        Assert.False(ok);
        Assert.True(await SecuriteService.VerifierCodeAsync(_db, "premier"));
        Assert.False(await SecuriteService.VerifierCodeAsync(_db, "second"));
    }

    [Fact]
    public async Task DefinirCode_ChangementAvecBonAncienCode_Reussit_EtAncienCodeNeMarchePlus()
    {
        await SecuriteService.DefinirCodeAsync(_db, "premier", null);

        var ok = await SecuriteService.DefinirCodeAsync(_db, "second", ancienCode: "premier");

        Assert.True(ok);
        Assert.True(await SecuriteService.VerifierCodeAsync(_db, "second"));
        Assert.False(await SecuriteService.VerifierCodeAsync(_db, "premier"));
    }
}
