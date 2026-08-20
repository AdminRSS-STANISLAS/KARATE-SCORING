namespace FkcScoring.Core.Data.Entities;

public class Participant
{
    public int Id { get; set; }
    public string Nom { get; set; } = "";
    public string Prenom { get; set; } = "";
    public int ClubId { get; set; }
    public Club? Club { get; set; }
    public string? Grade { get; set; }
    public DateTime? DateNaissance { get; set; }
    public string? NumeroLicence { get; set; }
    public string? Sexe { get; set; }
    public double? PoidsKg { get; set; }

    public string NomComplet => $"{Prenom} {Nom}".Trim();
}
