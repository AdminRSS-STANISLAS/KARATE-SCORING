using FkcScoring.Core.Data.Entities;

namespace FkcScoring.Core.Domain;

/// <summary>
/// Règles de décision d'un combat Kumite (cahier 5.4) : écart de points, fin de temps réglementaire,
/// règle du Senshu en poule, bascule vers Hantei, et disqualifications. Ne dépend d'aucune donnée
/// persistée : logique pure, testable indépendamment de l'UI et de la base.
/// </summary>
public static class CombatEngine
{
    public static void DemarrerCombat(Combat combat)
    {
        combat.Statut = StatutCombat.EnCours;
    }

    /// <summary>Ajoute un point (Ippon/Waza-ari/Yuko) et un événement horodaté ; retourne l'événement créé.</summary>
    public static EvenementCombat AjouterPoint(Combat combat, Couleur couleur, int points, TimeSpan tempsCombat)
    {
        if (combat.Statut != StatutCombat.EnCours)
            throw new InvalidOperationException("Le combat n'est pas en cours.");

        if (couleur == Couleur.Aka) combat.ScoreAka += points;
        else combat.ScoreAo += points;

        combat.SenshuCouleur ??= couleur; // règle du Senshu : premier point marqué

        var evenement = new EvenementCombat
        {
            CombatId = combat.Id,
            TempsCombat = tempsCombat,
            Type = TypeEvenement.Point,
            Couleur = couleur,
            Points = points
        };
        combat.Evenements.Add(evenement);
        return evenement;
    }

    /// <summary>Applique une pénalité. Hansoku/Shikkaku/Kiken mettent fin au combat immédiatement.</summary>
    public static EvenementCombat AppliquerPenalite(Combat combat, Couleur couleur, TypePenalite penalite, TimeSpan tempsCombat)
    {
        if (combat.Statut != StatutCombat.EnCours)
            throw new InvalidOperationException("Le combat n'est pas en cours.");

        var evenement = new EvenementCombat
        {
            CombatId = combat.Id,
            TempsCombat = tempsCombat,
            Type = TypeEvenement.Penalite,
            Couleur = couleur,
            Penalite = penalite
        };
        combat.Evenements.Add(evenement);

        if (penalite is TypePenalite.Hansoku or TypePenalite.Shikkaku or TypePenalite.Kiken)
        {
            combat.VainqueurCouleur = couleur == Couleur.Aka ? Couleur.Ao : Couleur.Aka;
            combat.ModeDecision = ModeDecision.Disqualification;
            combat.Statut = StatutCombat.Termine;
        }

        return evenement;
    }

    /// <summary>À appeler après chaque point : vérifie l'écart de victoire réglementaire. Retourne vrai si le combat est décidé.</summary>
    public static bool VerifierEcartVictoire(Combat combat, int ecartVictoire)
    {
        if (combat.Statut != StatutCombat.EnCours) return false;
        if (Math.Abs(combat.ScoreAka - combat.ScoreAo) < ecartVictoire) return false;

        combat.VainqueurCouleur = combat.ScoreAka > combat.ScoreAo ? Couleur.Aka : Couleur.Ao;
        combat.ModeDecision = ModeDecision.EcartPoints;
        combat.Statut = StatutCombat.Termine;
        return true;
    }

    /// <summary>
    /// À appeler à la fin du temps réglementaire. Si égalité : tranchée par le Senshu en poule, sinon
    /// le combat reste EnCours avec ModeDecision = Hantei en attente d'un vote arbitral (EnregistrerHantei).
    /// </summary>
    public static void TerminerParFinDeTemps(Combat combat, bool estEnPoule)
    {
        if (combat.ScoreAka != combat.ScoreAo)
        {
            combat.VainqueurCouleur = combat.ScoreAka > combat.ScoreAo ? Couleur.Aka : Couleur.Ao;
            combat.ModeDecision = ModeDecision.FinTemps;
            combat.Statut = StatutCombat.Termine;
        }
        else if (estEnPoule && combat.SenshuCouleur != null)
        {
            combat.VainqueurCouleur = combat.SenshuCouleur;
            combat.ModeDecision = ModeDecision.FinTemps;
            combat.Statut = StatutCombat.Termine;
        }
        else
        {
            combat.ModeDecision = ModeDecision.Hantei; // décision en attente, voir EnregistrerHantei
        }
    }

    /// <summary>Enregistre la décision arbitrale (vote de drapeaux Aka/Ao) en cas d'égalité hors poule.</summary>
    public static void EnregistrerHantei(Combat combat, Couleur vainqueur)
    {
        combat.VainqueurCouleur = vainqueur;
        combat.ModeDecision = ModeDecision.Hantei;
        combat.Statut = StatutCombat.Termine;
    }
}
