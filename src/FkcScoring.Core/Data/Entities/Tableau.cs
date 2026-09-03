namespace FkcScoring.Core.Data.Entities;

public class Tableau
{
    public int Id { get; set; }
    public int CategorieId { get; set; }
    public Categorie? Categorie { get; set; }
    public FormatTableau Format { get; set; }
    public DateTime CreatedAt { get; set; } = DateTime.Now;

    /// <summary>Aire (tatami) sur laquelle ce tableau se déroule ; null tant que l'organisateur ne l'a pas assigné.</summary>
    public int? AireId { get; set; }
    public Aire? Aire { get; set; }

    public List<Combat> Combats { get; set; } = new();
    public List<KataConfrontation> KataConfrontations { get; set; } = new();
}
