namespace FkcScoring.App.Services;

/// <summary>État partagé minimal entre les écrans : l'application gère une compétition à la fois.</summary>
public static class AppState
{
    public static int? CompetitionId { get; set; }
}
