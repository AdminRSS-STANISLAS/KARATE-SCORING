namespace FkcScoring.Core.Data.Entities;

public class Categorie
{
    public int Id { get; set; }
    public int CompetitionId { get; set; }
    public Competition? Competition { get; set; }
    public string Nom { get; set; } = "";
    public Discipline Discipline { get; set; } = Discipline.KumiteIndividuel;
    public int? AgeMin { get; set; }
    public int? AgeMax { get; set; }
    public string? Sexe { get; set; }
    public string? GradeMin { get; set; }
    public string? GradeMax { get; set; }
    public double? PoidsMin { get; set; }
    public double? PoidsMax { get; set; }

    // Surcharges optionnelles des paramètres par défaut de la compétition
    public int? DureeCombatSec { get; set; }
    public int? NbJugesKata { get; set; }

    public List<Inscription> Inscriptions { get; set; } = new();
    public List<Equipe> Equipes { get; set; } = new();
    public List<Tableau> Tableaux { get; set; } = new();
}
