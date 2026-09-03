namespace FkcScoring.Core.Data.Entities;

/// <summary>
/// Aire de compétition (tatami) : permet de faire tourner plusieurs tableaux en parallèle sur des
/// postes de scoring distincts (cahier 5.1, "plusieurs aires de compétition en parallèle").
/// </summary>
public class Aire
{
    public int Id { get; set; }
    public int CompetitionId { get; set; }
    public Competition? Competition { get; set; }
    public string Nom { get; set; } = "";
    public int Ordre { get; set; }
}
