namespace FkcScoring.Core.Data;

/// <summary>
/// Résout où vit la base SQLite de l'API web. <c>KARATE_SCORING_DB_PATH</c> permet de surcharger ce
/// chemin (utile en conteneur, ex. Docker), sinon on retombe sur un dossier utilisateur standard —
/// contrairement à un chemin de style "/data/..." qui n'a de sens que dans un conteneur Linux et
/// échoue en exécution native Windows faute de droits sur la racine du disque.
/// </summary>
public static class FkcScoringPaths
{
    public static string ResolveDbPath() =>
        Environment.GetEnvironmentVariable("KARATE_SCORING_DB_PATH") ?? Path.Combine(DefaultDataDir(), "karate-scoring.db");

    public static string ResolveBackupDir(string dbPath) =>
        Path.Combine(Path.GetDirectoryName(dbPath) ?? DefaultDataDir(), "backups");

    /// <summary>Dossier des images importées (photos d'athlètes, logos de clubs) — à côté de la base, pas dans wwwroot (contenu utilisateur, pas un asset de l'application).</summary>
    public static string ResolveUploadsDir(string dbPath) =>
        Path.Combine(Path.GetDirectoryName(dbPath) ?? DefaultDataDir(), "uploads");

    private static string DefaultDataDir() =>
        Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "KarateScoring");
}
