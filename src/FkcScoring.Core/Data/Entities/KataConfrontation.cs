namespace FkcScoring.Core.Data.Entities;

/// <summary>
/// Confrontation directe entre deux compétiteurs (ou deux équipes) en Kata, tranchée par un vote
/// à drapeaux du jury — équivalent Kata du Combat en Kumite. Module différé à une itération suivante.
/// </summary>
public class KataConfrontation
{
    public int Id { get; set; }
    public int TableauId { get; set; }
    public Tableau? Tableau { get; set; }
    public int Tour { get; set; }

    public int? Participant1Id { get; set; }
    public Participant? Participant1 { get; set; }
    public int? Equipe1Id { get; set; }
    public Equipe? Equipe1 { get; set; }

    public int? Participant2Id { get; set; }
    public Participant? Participant2 { get; set; }
    public int? Equipe2Id { get; set; }
    public Equipe? Equipe2 { get; set; }

    public int? Kata1Id { get; set; }
    public Kata? Kata1 { get; set; }
    public int? Kata2Id { get; set; }
    public Kata? Kata2 { get; set; }

    public int NbJuges { get; set; }
    public Couleur? VainqueurCouleur { get; set; }

    public List<VoteJuge> Votes { get; set; } = new();
}

public class VoteJuge
{
    public int Id { get; set; }
    public int ConfrontationId { get; set; }
    public KataConfrontation? Confrontation { get; set; }
    public int JugeNumero { get; set; }
    public Couleur VoteCouleur { get; set; }
}
