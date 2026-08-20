namespace FkcScoring.Core.Data.Entities;

public class Tableau
{
    public int Id { get; set; }
    public int CategorieId { get; set; }
    public Categorie? Categorie { get; set; }
    public FormatTableau Format { get; set; }
    public DateTime CreatedAt { get; set; } = DateTime.Now;

    public List<Combat> Combats { get; set; } = new();
    public List<KataConfrontation> KataConfrontations { get; set; } = new();
}
