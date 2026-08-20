namespace FkcScoring.Core.Data.Entities;

public class Inscription
{
    public int Id { get; set; }
    public int CategorieId { get; set; }
    public Categorie? Categorie { get; set; }
    public int? ParticipantId { get; set; }
    public Participant? Participant { get; set; }
    public int? EquipeId { get; set; }
    public Equipe? Equipe { get; set; }
    public StatutInscription Statut { get; set; } = StatutInscription.Inscrit;
}
