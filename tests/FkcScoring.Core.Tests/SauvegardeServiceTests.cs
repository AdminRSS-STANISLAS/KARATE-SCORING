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

    [Fact]
    public void ArchiveTransfert_RoundTrip_RestaureLaBaseEtLesPhotos()
    {
        // Découvert en simulant un vrai transfert de compétition entre deux postes : l'ancien format
        // "juste la base" perdait silencieusement toutes les photos (fichiers séparés sur disque, jamais
        // dans le contenu SQLite) — ce test couvre exactement ce chemin pour que ça ne repasse jamais inaperçu.
        var uploadsDir = Path.Combine(_dossierTemp, "uploads");
        Directory.CreateDirectory(uploadsDir);
        var cheminPhoto = Path.Combine(uploadsDir, "participant-1.png");
        var octetsPhoto = new byte[] { 0x89, 0x50, 0x4E, 0x47, 1, 2, 3, 4 };
        File.WriteAllBytes(cheminPhoto, octetsPhoto);

        byte[] archive;
        using (var db = CreerContexte())
        {
            db.Competitions.Add(new Competition { Nom = "À transférer", Date = DateTime.Today });
            db.SaveChanges();
            archive = SauvegardeService.CreerArchiveTransfert(db, uploadsDir, Path.Combine(_dossierTemp, "export-temp"));
        }
        Assert.Equal((byte)'P', archive[0]);
        Assert.Equal((byte)'K', archive[1]);

        // Simule l'arrivée sur un second poste : dossiers vides, comme après une installation neuve.
        var dbPathCible = Path.Combine(_dossierTemp, "poste2", "karate-scoring.db");
        var uploadsCible = Path.Combine(_dossierTemp, "poste2", "uploads");
        Directory.CreateDirectory(Path.GetDirectoryName(dbPathCible)!);
        var optionsVide = new DbContextOptionsBuilder<FkcScoringContext>().UseSqlite($"Data Source={dbPathCible}").Options;
        using (var dbVide = new FkcScoringContext(optionsVide)) dbVide.Database.EnsureCreated();

        SauvegardeService.RestaurerArchiveTransfert(dbPathCible, uploadsCible, archive, Path.Combine(_dossierTemp, "import-temp"));

        var optionsApres = new DbContextOptionsBuilder<FkcScoringContext>().UseSqlite($"Data Source={dbPathCible}").Options;
        using (var dbApres = new FkcScoringContext(optionsApres))
            Assert.Equal("À transférer", dbApres.Competitions.Single().Nom);

        var photoRestauree = Path.Combine(uploadsCible, "participant-1.png");
        Assert.True(File.Exists(photoRestauree));
        Assert.Equal(octetsPhoto, File.ReadAllBytes(photoRestauree));
    }

    [Fact]
    public void RestaurerArchiveTransfert_AccepteAussiUnSimpleFichierDbHerite()
    {
        using (var db = CreerContexte())
        {
            db.Competitions.Add(new Competition { Nom = "Ancien format", Date = DateTime.Today });
            db.SaveChanges();
        }
        var ancienExport = File.ReadAllBytes(SauvegardeService.SauvegarderVersFichier(CreerContexte(), _backupDir, "auto"));

        var dbCible = Path.Combine(_dossierTemp, "poste3", "karate-scoring.db");
        Directory.CreateDirectory(Path.GetDirectoryName(dbCible)!);
        var options = new DbContextOptionsBuilder<FkcScoringContext>().UseSqlite($"Data Source={dbCible}").Options;
        using (var dbVide = new FkcScoringContext(options)) dbVide.Database.EnsureCreated();

        SauvegardeService.RestaurerArchiveTransfert(dbCible, Path.Combine(_dossierTemp, "poste3", "uploads"), ancienExport, Path.Combine(_dossierTemp, "import-temp2"));

        using var dbApres = new FkcScoringContext(options);
        Assert.Equal("Ancien format", dbApres.Competitions.Single().Nom);
    }
}
