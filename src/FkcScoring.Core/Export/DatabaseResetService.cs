using FkcScoring.Core.Data;

namespace FkcScoring.Core.Export;

/// <summary>
/// Filet de sécurité pour le bouton "Exporter et réinitialiser" : sauvegarde horodatée du fichier
/// SQLite avant de vider les données de la compétition en cours, pour repartir sur une base propre
/// sans jamais perdre la trace de ce qui a été joué.
/// </summary>
public class DatabaseResetService
{
    private readonly string _dbPath;
    private readonly string _backupDir;

    public DatabaseResetService(string dbPath, string backupDir)
    {
        _dbPath = dbPath;
        _backupDir = backupDir;
    }

    /// <summary>Copie le fichier .db vers le dossier de sauvegarde et retourne le chemin de la copie.</summary>
    public string SauvegarderBaseVersFichier()
    {
        Directory.CreateDirectory(_backupDir);
        var backupPath = Path.Combine(_backupDir, $"fkc_scoring_{DateTime.Now:yyyyMMdd_HHmmss}.db");
        File.Copy(_dbPath, backupPath, overwrite: false);
        return backupPath;
    }

    /// <summary>Vide toutes les données de compétition (une compétition à la fois, pas d'historique conservé en base).</summary>
    public void ViderCompetition(FkcScoringContext db)
    {
        db.Classements.RemoveRange(db.Classements);
        db.VotesJuges.RemoveRange(db.VotesJuges);
        db.KataConfrontations.RemoveRange(db.KataConfrontations);
        db.EvenementsCombat.RemoveRange(db.EvenementsCombat);
        db.Combats.RemoveRange(db.Combats);
        db.Tableaux.RemoveRange(db.Tableaux);
        db.EquipeMembres.RemoveRange(db.EquipeMembres);
        db.Inscriptions.RemoveRange(db.Inscriptions);
        db.Equipes.RemoveRange(db.Equipes);
        db.Categories.RemoveRange(db.Categories);
        db.Competitions.RemoveRange(db.Competitions);
        db.Participants.RemoveRange(db.Participants);
        db.Clubs.RemoveRange(db.Clubs);
        db.AuditLogs.RemoveRange(db.AuditLogs);
        db.SaveChanges();
    }
}
