namespace FkcScoring.Core.Data.Entities;

public class Equipe
{
    public int Id { get; set; }
    public int CategorieId { get; set; }
    public Categorie? Categorie { get; set; }
    public string Nom { get; set; } = "";
    public int? ClubId { get; set; }
    public Club? Club { get; set; }

    public List<EquipeMembre> Membres { get; set; } = new();
}

public class EquipeMembre
{
    public int Id { get; set; }
    public int EquipeId { get; set; }
    public Equipe? Equipe { get; set; }
    public int ParticipantId { get; set; }
    public Participant? Participant { get; set; }
}
