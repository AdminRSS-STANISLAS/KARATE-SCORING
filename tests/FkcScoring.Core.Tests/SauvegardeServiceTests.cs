using FkcScoring.Core.Data;
using FkcScoring.Core.Data.Entities;
using FkcScoring.Core.Export;
using Microsoft.EntityFrameworkCore;
using Xunit;

namespace FkcScoring.Core.Tests;

/// <summary>Comme DatabaseResetServiceTests, a besoin d'un vrai fichier .db sur disque (VACUUM INTO, File.Copy).</summary>
public class SauvegardeServiceTests : IDisposable
{
    private readonly string _dossierTemp;
    private readonly string _dbPath;
    private readonly string _backupDir;

    public SauvegardeServiceTests()
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
    public void SauvegarderVersFichier_ProduitUnFichierValideEtNonVide()
    {
        using var db = CreerContexte();
        db.Competitions.Add(new Competition { Nom = "Test", Date = DateTime.Today });
        db.SaveChanges();

        var chemin = SauvegardeService.SauvegarderVersFichier(db, _backupDir, "auto");

        Assert.True(File.Exists(chemin));
        Assert.True(new FileInfo(chemin).Length > 0);
        Assert.True(SauvegardeService.EstFichierSqliteValide(chemin));
    }

    [Fact]
    public void PurgerAnciennes_NeGardeQueLesPlusRecentesDuMemePrefixe()
    {
        using var db = CreerContexte();
        for (int i = 0; i < 5; i++) SauvegardeService.SauvegarderVersFichier(db, _backupDir, "auto");
        var autreCombat = SauvegardeService.SauvegarderVersFichier(db, _backupDir, "avant_restauration");

        SauvegardeService.PurgerAnciennes(_backupDir, "auto", nbAConserver: 2);

        var restantes = SauvegardeService.ListerSauvegardes(_backupDir);
        Assert.Equal(3, restantes.Count); // 2 "auto" conservées + 1 "avant_restauration" jamais touchée par la purge "auto"
        Assert.Contains(restantes, s => s.Nom == Path.GetFileName(autreCombat));
    }

    [Fact]
    public void Restaurer_RemetLaBaseDansLEtatDeLaSauvegarde()
    {
        using (var db = CreerContexte())
        {
            db.Competitions.Add(new Competition { Nom = "Compétition A", Date = DateTime.Today });
            db.SaveChanges();
        }

        string sauvegardeA;
        using (var db = CreerContexte())
            sauvegardeA = SauvegardeService.SauvegarderVersFichier(db, _backupDir, "auto");

        using (var db = CreerContexte())
        {
            db.Competitions.Add(new Competition { Nom = "Compétition B", Date = DateTime.Today });
            db.SaveChanges();
            Assert.Equal(2, db.Competitions.Count());
        }

        SauvegardeService.Restaurer(_dbPath, sauvegardeA);

        using var apres = CreerContexte();
        var noms = apres.Competitions.Select(c => c.Nom).ToList();
        Assert.Single(noms);
        Assert.Equal("Compétition A", noms[0]);
    }

    [Fact]
    public void EstFichierSqliteValide_FichierQuelconque_Faux()
    {
        var cheminBidon = Path.Combine(_dossierTemp, "pas-une-base.db");
        File.WriteAllText(cheminBidon, "ceci n'est pas une base SQLite");

        Assert.False(SauvegardeService.EstFichierSqliteValide(cheminBidon));
    }
}
