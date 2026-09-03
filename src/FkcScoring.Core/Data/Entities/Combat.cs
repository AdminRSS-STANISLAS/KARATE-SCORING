namespace FkcScoring.Core.Data.Entities;

public class Combat
{
    public int Id { get; set; }
    public int TableauId { get; set; }
    public Tableau? Tableau { get; set; }
    public int Tour { get; set; }
    public string? Aire { get; set; }

    public int? CompetiteurAkaId { get; set; }
    public Participant? CompetiteurAka { get; set; }
    public int? CompetiteurAoId { get; set; }
    public Participant? CompetiteurAo { get; set; }

    public string? ArbitreNom { get; set; }
    public int ScoreAka { get; set; }
    public int ScoreAo { get; set; }
    public Couleur? SenshuCouleur { get; set; }
    public ModeDecision? ModeDecision { get; set; }
    public Couleur? VainqueurCouleur { get; set; }
    public int? DureeReelleSec { get; set; }
    public StatutCombat Statut { get; set; } = StatutCombat.EnAttente;

    /// <summary>Jeton de concurrence (incrémenté à chaque écriture) : détecte deux postes qui touchent le même combat en parallèle.</summary>
    public int RowVersion { get; set; }

    /// <summary>Vrai si ce combat n'a pas été disputé car un des deux compétiteurs était exempté (bye).</summary>
    public bool EstBye { get; set; }

    /// <summary>Vrai si ce combat fait partie d'un mini-tableau de repêchage (et non du tableau principal).</summary>
    public bool EstRepechage { get; set; }

    /// <summary>Moitié du tableau (1 ou 2) à laquelle appartient ce combat ; null pour la finale, qui fusionne les deux moitiés.</summary>
    public int? Moitie { get; set; }

    /// <summary>Combat du tour suivant dans lequel le vainqueur de celui-ci est engagé.</summary>
    public int? ProchainCombatId { get; set; }
    public Combat? ProchainCombat { get; set; }

    /// <summary>Couleur (créneau) occupée par le vainqueur de ce combat dans le combat suivant.</summary>
    public Couleur? ProchainCombatCouleur { get; set; }

    public List<EvenementCombat> Evenements { get; set; } = new();

    public int? VainqueurParticipantId => VainqueurCouleur switch
    {
        Couleur.Aka => CompetiteurAkaId,
        Couleur.Ao => CompetiteurAoId,
        _ => null
    };
}
