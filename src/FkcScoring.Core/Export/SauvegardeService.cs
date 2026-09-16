using System.IO.Compression;
using FkcScoring.Core.Data;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;

namespace FkcScoring.Core.Export;

public record SauvegardeInfo(string Nom, DateTime CreeLe, long TailleOctets);

/// <summary>
/// Sauvegarde/restauration de la base SQLite pendant que l'application est activement utilisée
/// (cahier §25) — distinct de <see cref="DatabaseResetService"/>, dont la sauvegarde par simple
/// File.Copy n'est sûre que juste avant un vidage complet (pas d'écriture concurrente à ce moment-là).
/// </summary>
public static class SauvegardeService
{
    /// <summary>
    /// Copie transactionnellement cohérente de la base courante, même si des écritures sont en cours
    /// (VACUUM INTO est une commande SQLite native prévue pour ça — contrairement à un File.Copy brut,
    /// qui risquerait de capturer un fichier à moitié écrit).
    /// </summary>
    public static string SauvegarderVersFichier(FkcScoringContext db, string backupDir, string prefixe)
    {
        Directory.CreateDirectory(backupDir);
        // VACUUM INTO échoue si le fichier cible existe déjà : au-delà de la précision à la seconde,
        // un suffixe court évite toute collision entre deux sauvegardes déclenchées rapprochées
        // (ex. double-clic sur "Télécharger", ou plusieurs tests dans la même seconde).
        var suffixe = Guid.NewGuid().ToString("N")[..8];
        var chemin = Path.Combine(backupDir, $"{prefixe}_{DateTime.Now:yyyyMMdd_HHmmss}_{suffixe}.db");
        // VACUUM INTO n'accepte pas de paramètre lié pour le nom de fichier en SQLite (contrairement à
        // une requête normale) ; le chemin est entièrement généré côté serveur ci-dessus (jamais dérivé
        // d'une entrée utilisateur), donc l'échappement manuel des apostrophes suffit ici.
#pragma warning disable EF1002
        db.Database.ExecuteSqlRaw($"VACUUM INTO '{chemin.Replace("'", "''")}'");
#pragma warning restore EF1002
        return chemin;
    }

    public static List<SauvegardeInfo> ListerSauvegardes(string backupDir)
    {
        if (!Directory.Exists(backupDir)) return new List<SauvegardeInfo>();
        return Directory.GetFiles(backupDir, "*.db")
            .Select(f => new FileInfo(f))
            .OrderByDescending(f => f.LastWriteTime)
            .Select(f => new SauvegardeInfo(f.Name, f.LastWriteTime, f.Length))
            .ToList();
    }

    /// <summary>Ne conserve que les <paramref name="nbAConserver"/> sauvegardes les plus récentes du préfixe donné, pour ne pas remplir le disque sur un poste qui tourne des heures.</summary>
    public static void PurgerAnciennes(string backupDir, string prefixe, int nbAConserver)
    {
        if (!Directory.Exists(backupDir)) return;
        var fichiers = Directory.GetFiles(backupDir, $"{prefixe}_*.db")
            .Select(f => new FileInfo(f))
            .OrderByDescending(f => f.LastWriteTime)
            .Skip(nbAConserver);
        foreach (var f in fichiers) f.Delete();
    }

    /// <summary>Remplace le fichier de base courant par <paramref name="cheminSource"/>. Vide d'abord le pool de connexions natif Sqlite pour libérer le verrou de fichier — le DbContext étant scoped (une instance par requête), la requête HTTP suivante rouvre naturellement une connexion vers le fichier remplacé.</summary>
    public static void Restaurer(string dbPath, string cheminSource)
    {
        SqliteConnection.ClearAllPools();
        File.Copy(cheminSource, dbPath, overwrite: true);
    }

    /// <summary>Rejette un fichier qui ne serait pas une vraie base de l'application avant d'écraser quoi que ce soit avec (import externe).</summary>
    public static bool EstFichierSqliteValide(string chemin)
    {
        try
        {
            using var connection = new SqliteConnection($"Data Source={chemin};Mode=ReadOnly");
            connection.Open();
            using var cmd = connection.CreateCommand();
            // Vérifie une table métier plutôt que __EFMigrationsHistory : cette dernière n'existe que si
            // le schéma a été créé via Migrate() (jamais le cas avec EnsureCreated(), utilisé partout
            // dans les tests de ce projet) — Competitions, elle, existe quel que soit le mode de création.
            cmd.CommandText = "SELECT COUNT(*) FROM sqlite_master WHERE type='table' AND name='Competitions'";
            return Convert.ToInt64(cmd.ExecuteScalar()) > 0;
        }
        catch (SqliteException)
        {
            return false;
        }
    }

    /// <summary>
    /// Package la base + les photos/logos importés (uploads/) dans une archive unique, transférable telle
    /// quelle vers un autre poste Karate Scoring (clé USB, partage réseau) — un export "juste la base" (le
    /// seul format avant cette fonctionnalité) perdait silencieusement toutes les photos, puisqu'elles
    /// vivent en fichiers séparés sur disque, pas dans le contenu de la base SQLite elle-même.
    /// </summary>
    public static byte[] CreerArchiveTransfert(FkcScoringContext db, string uploadsDir, string tempDir)
    {
        Directory.CreateDirectory(tempDir);
        var dbTemp = SauvegarderVersFichier(db, tempDir, "export");
        var zipTemp = Path.Combine(tempDir, $"export_{Guid.NewGuid():N}.zip");
        try
        {
            using (var zip = ZipFile.Open(zipTemp, ZipArchiveMode.Create))
            {
                zip.CreateEntryFromFile(dbTemp, "karate-scoring.db");
                if (Directory.Exists(uploadsDir))
                    foreach (var fichier in Directory.GetFiles(uploadsDir))
                        zip.CreateEntryFromFile(fichier, Path.Combine("uploads", Path.GetFileName(fichier)));
            }
            return File.ReadAllBytes(zipTemp);
        }
        finally
        {
            if (File.Exists(dbTemp)) File.Delete(dbTemp);
            if (File.Exists(zipTemp)) File.Delete(zipTemp);
        }
    }

    /// <summary>
    /// Restaure depuis une archive de transfert (base + photos/logos) produite par <see cref="CreerArchiveTransfert"/>.
    /// Accepte aussi un simple fichier .db (anciens exports, ou sauvegarde automatique) : dans ce cas les
    /// photos ne sont pas touchées — c'est déjà le comportement historique de <see cref="Restaurer"/>.
    /// </summary>
    public static void RestaurerArchiveTransfert(string dbPath, string uploadsDir, byte[] zipOuDb, string tempDir)
    {
        Directory.CreateDirectory(tempDir);
        var estZip = zipOuDb.Length >= 2 && zipOuDb[0] == 'P' && zipOuDb[1] == 'K';
        if (!estZip)
        {
            var tempDb = Path.Combine(tempDir, $"import_{Guid.NewGuid():N}.db");
            File.WriteAllBytes(tempDb, zipOuDb);
            try { Restaurer(dbPath, tempDb); } finally { if (File.Exists(tempDb)) File.Delete(tempDb); }
            return;
        }

        var zipTemp = Path.Combine(tempDir, $"import_{Guid.NewGuid():N}.zip");
        var extractDir = Path.Combine(tempDir, $"import_{Guid.NewGuid():N}");
        try
        {
            File.WriteAllBytes(zipTemp, zipOuDb);
            ZipFile.ExtractToDirectory(zipTemp, extractDir);
            var dbExtrait = Path.Combine(extractDir, "karate-scoring.db");
            if (!File.Exists(dbExtrait)) throw new InvalidOperationException("Archive invalide : base de données introuvable.");
            Restaurer(dbPath, dbExtrait);

            var uploadsExtrait = Path.Combine(extractDir, "uploads");
            if (Directory.Exists(uploadsExtrait))
            {
                Directory.CreateDirectory(uploadsDir);
                foreach (var fichier in Directory.GetFiles(uploadsExtrait))
                    File.Copy(fichier, Path.Combine(uploadsDir, Path.GetFileName(fichier)), overwrite: true);
            }
        }
        finally
        {
            if (File.Exists(zipTemp)) File.Delete(zipTemp);
            if (Directory.Exists(extractDir)) Directory.Delete(extractDir, recursive: true);
        }
    }
}
