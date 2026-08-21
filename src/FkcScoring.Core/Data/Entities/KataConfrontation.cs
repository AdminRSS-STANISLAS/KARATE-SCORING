namespace FkcScoring.Core.Data.Entities;

/// <summary>
/// Confrontation directe entre deux compétiteurs (ou deux équipes) en Kata, tranchée par un vote
/// à drapeaux du jury — équivalent Kata du Combat en Kumite. Suit le même tableau (poule /
/// élimination / repêchage à deux bronzes, cahier 5.5) que TableauService/Combat : les champs
/// Statut/EstBye/EstRepechage/Moitie/Prochain* jouent ici exactement le même rôle.
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
    public StatutCombat Statut { get; set; } = StatutCombat.EnAttente;

    /// <summary>Vrai si cette confrontation n'a pas eu lieu car un des deux compétiteurs était exempté (bye).</summary>
    public bool EstBye { get; set; }

    /// <summary>Vrai si cette confrontation fait partie d'un mini-tableau de repêchage (et non du tableau principal).</summary>
    public bool EstRepechage { get; set; }

    /// <summary>Moitié du tableau (1 ou 2) à laquelle appartient cette confrontation ; null pour la finale.</summary>
    public int? Moitie { get; set; }

    /// <summary>Confrontation du tour suivant dans laquelle le vainqueur de celle-ci est engagé.</summary>
    public int? ProchainConfrontationId { get; set; }
    public KataConfrontation? ProchainConfrontation { get; set; }

    /// <summary>Couleur (créneau) occupée par le vainqueur de cette confrontation dans la suivante.</summary>
    public Couleur? ProchainConfrontationCouleur { get; set; }

    public List<VoteJuge> Votes { get; set; } = new();

    public int? VainqueurParticipantId => VainqueurCouleur switch
    {
        Couleur.Aka => Participant1Id,
        Couleur.Ao => Participant2Id,
        _ => null
    };

    public int? VainqueurEquipeId => VainqueurCouleur switch
    {
        Couleur.Aka => Equipe1Id,
        Couleur.Ao => Equipe2Id,
        _ => null
    };
}

public class VoteJuge
{
    public int Id { get; set; }
    public int ConfrontationId { get; set; }
    public KataConfrontation? Confrontation { get; set; }
    public int JugeNumero { get; set; }
    public Couleur VoteCouleur { get; set; }
}
