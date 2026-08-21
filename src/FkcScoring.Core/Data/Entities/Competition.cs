namespace FkcScoring.Core.Data.Entities;

public class Competition
{
    public int Id { get; set; }
    public string Nom { get; set; } = "";
    public DateTime Date { get; set; } = DateTime.Today;
    public string? Lieu { get; set; }
    public NiveauCompetition Niveau { get; set; } = NiveauCompetition.Club;
    public string Reglement { get; set; } = "WKF";

    // Barème paramétrable (évolue avec les règlements fédéraux, jamais figé dans le code)
    public int PointsIppon { get; set; } = 3;
    public int PointsWazaAri { get; set; } = 2;
    public int PointsYuko { get; set; } = 1;
    public int EcartVictoire { get; set; } = 8;
    public int DureeCombatDefautSec { get; set; } = 180;
    public int NbJugesKataDefaut { get; set; } = 5;

    // Seuils de format de tableau (cahier 5.3), ajustables par l'organisateur.
    public int SeuilPouleUnique { get; set; } = 4;
    public int SeuilPoulePuisElimination { get; set; } = 7;

    public DateTime CreatedAt { get; set; } = DateTime.Now;

    public List<Categorie> Categories { get; set; } = new();
}
