using System.IO;
using System.Text.Json;

namespace KarateScoring.Desktop.Services;

/// <summary>
/// Où KARATE SCORING range ses données (base SQLite, sauvegardes, photos/logos importés) — choisi une
/// fois par l'organisateur au premier lancement, un peu comme l'emplacement de sauvegarde d'un jeu
/// vidéo local. Ce choix lui-même est mémorisé dans %APPDATA% (pas dans le dossier de données choisi,
/// puisque c'est justement ce qu'on est en train de déterminer) et réutilisé à chaque démarrage suivant.
/// </summary>
public static class DataFolderSettings
{
    private static readonly string SettingsPath =
        Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData), "KarateScoring", "shell-settings.json");

    // Documents plutôt que %LOCALAPPDATA% : l'organisateur doit pouvoir retrouver facilement ce dossier
    // pour le copier à la main (clé USB, partage réseau) vers un autre poste Karate Scoring — %LOCALAPPDATA%
    // est caché et peu naturel à parcourir pour un usage "copier mes données ailleurs" façon jeu vidéo local.
    private static readonly string DefaultDataFolder =
        Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.MyDocuments), "KarateScoring");

    public static string? LireDossierConfigure()
    {
        if (!File.Exists(SettingsPath)) return null;
        try
        {
            var json = File.ReadAllText(SettingsPath);
            var doc = JsonSerializer.Deserialize<ShellSettings>(json);
            return doc?.DossierDonnees;
        }
        catch { return null; }
    }

    public static void Enregistrer(string dossierDonnees)
    {
        Directory.CreateDirectory(Path.GetDirectoryName(SettingsPath)!);
        var json = JsonSerializer.Serialize(new ShellSettings { DossierDonnees = dossierDonnees }, new JsonSerializerOptions { WriteIndented = true });
        File.WriteAllText(SettingsPath, json);
    }

    /// <summary>Emplacement par défaut proposé dans le sélecteur de dossier au premier lancement.</summary>
    public static string SuggestionParDefaut() => DefaultDataFolder;

    private class ShellSettings
    {
        public string? DossierDonnees { get; set; }
    }
}
