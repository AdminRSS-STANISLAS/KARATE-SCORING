namespace FkcScoring.Core.Data.Entities;

public class EvenementCombat
{
    public int Id { get; set; }
    public int CombatId { get; set; }
    public Combat? Combat { get; set; }

    /// <summary>Temps écoulé dans le combat au moment de l'événement (pour l'historique horodaté).</summary>
    public TimeSpan TempsCombat { get; set; }
    public DateTime Horodatage { get; set; } = DateTime.Now;

    public TypeEvenement Type { get; set; }
    public Couleur Couleur { get; set; }

    /// <summary>Renseigné si Type = Point (1, 2 ou 3).</summary>
    public int? Points { get; set; }

    /// <summary>Renseigné si Type = Penalite.</summary>
    public TypePenalite? Penalite { get; set; }
}
