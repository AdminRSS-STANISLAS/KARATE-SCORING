namespace FkcScoring.App.Services;

/// <summary>
/// État partagé entre l'écran d'arbitrage (qui l'alimente) et l'écran de diffusion publique (qui
/// l'affiche en grand sur la TV). Évite d'interroger la base depuis la fenêtre de diffusion : elle
/// se contente de refléter ce que l'arbitre voit, en temps réel.
/// </summary>
public class ScoreboardSnapshot
{
    public bool CombatActif { get; set; }
    public string NomAka { get; set; } = "";
    public string ClubAka { get; set; } = "";
    public string NomAo { get; set; } = "";
    public string ClubAo { get; set; } = "";
    public int ScoreAka { get; set; }
    public int ScoreAo { get; set; }
    public string Chrono { get; set; } = "00:00";
    public bool ChronoEnCours { get; set; }
    public string? Categorie { get; set; }
    public string? EtatCombat { get; set; }
}

public static class LiveScoreboardState
{
    public static ScoreboardSnapshot Actuel { get; private set; } = new();

    public static event EventHandler? Changed;

    public static void Publier(ScoreboardSnapshot snapshot)
    {
        Actuel = snapshot;
        Changed?.Invoke(null, EventArgs.Empty);
    }
}
