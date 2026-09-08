using FkcScoring.Core.Data;
using FkcScoring.Core.Data.Entities;
using FkcScoring.Core.Export;
using Microsoft.EntityFrameworkCore;
using Xunit;

namespace FkcScoring.Core.Tests;

/// <summary>
/// DatabaseResetService écrit/lit un vrai fichier .db (File.Copy) — contrairement aux autres tests de
/// ce projet, qui utilisent une base Sqlite en mémoire, celui-ci a besoin d'un fichier réel sur disque.
/// </summary>
public class DatabaseResetServiceTests : IDisposable
{
    private readonly string _dossierTemp;
    private readonly string _dbPath;
    private readonly string _backupDir;

    public DatabaseResetServiceTests()
    {
        _dossierTemp = Path.Combine(Path.GetTempPath(), "fkc-scoring-tests-" + Guid.NewGuid());
        Directory.CreateDirectory(_dossierTemp);
        _dbPath = Path.Combine(_dossierTemp, "test.db");
        _backupDir = Path.Combine(_dossierTemp, "backups");
    }

    public void Dispose()
    {
        try { Directory.Delete(_dossierTemp, recursive: true); } catch { /* nettoyage best-effort */ }
    }

    private FkcScoringContext CreerContexte()
    {
        var options = new DbContextOptionsBuilder<FkcScoringContext>().UseSqlite($"Data Source={_dbPath}").Options;
        var db = new FkcScoringContext(options);
        db.Database.EnsureCreated();
        return db;
    }

    [Fact]
    public void SauvegarderBaseVersFichier_CreeUneCopieHorodateeDuFichierDb()
    {
        using var db = CreerContexte();
        db.Competitions.Add(new Competition { Nom = "Test", Date = DateTime.Today });
        db.SaveChanges();

        var service = new DatabaseResetService(_dbPath, _backupDir);
        var backupPath = service.SauvegarderBaseVersFichier();

        Assert.True(File.Exists(backupPath));
        Assert.True(new FileInfo(backupPath).Length > 0);
    }

    [Fact]
    public void ViderCompetition_SupprimeLesDonneesDeCompetition_MaisPasLesParametresMachine()
    {
        using var db = CreerContexte();
        db.Competitions.Add(new Competition { Nom = "Test", Date = DateTime.Today });
        db.Clubs.Add(new Club { Nom = "FKC" });
        // Le code administrateur (Phase 7) est un réglage machine, pas une donnée de compétition :
        // un reset ne doit surtout pas effacer sa propre protection.
        db.Parametres.Add(new Parametres { Id = 1, AdminCodeHash = "1.abc.def" });
        db.SaveChanges();

        var service = new DatabaseResetService(_dbPath, _backupDir);
        service.ViderCompetition(db);

        Assert.Empty(db.Competitions);
        Assert.Empty(db.Clubs);
        Assert.Single(db.Parametres);
        Assert.Equal("1.abc.def", db.Parametres.Single().AdminCodeHash);
    }
}
