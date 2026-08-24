using FkcScoring.Core.Data.Entities;

namespace FkcScoring.Core.Domain;

/// <summary>
/// Règles de décision d'une confrontation Kata (cahier 5.5) : saisie du kata exécuté (par le juge
/// central) puis vote à drapeaux de chaque juge, vainqueur désigné par majorité de drapeaux. Logique
/// pure, ne dépend d'aucune donnée persistée au-delà de l'entité passée en paramètre — testable
/// indépendamment de l'UI et de la base, comme CombatEngine pour le Kumite.
/// </summary>
public static class KataEngine
{
    public static void DefinirKata(KataConfrontation confrontation, Couleur couleur, int kataId)
    {
        if (couleur == Couleur.Aka) confrontation.Kata1Id = kataId;
        else confrontation.Kata2Id = kataId;
    }

    /// <summary>Enregistre (ou met à jour) le drapeau levé par un juge. Ne tranche pas la confrontation — voir ValiderResultat.</summary>
    public static VoteJuge EnregistrerVote(KataConfrontation confrontation, int jugeNumero, Couleur couleur)
    {
        if (confrontation.Statut == StatutCombat.Termine)
            throw new InvalidOperationException("La confrontation est déjà tranchée.");
        if (jugeNumero < 1 || jugeNumero > confrontation.NbJuges)
            throw new ArgumentOutOfRangeException(nameof(jugeNumero), "Numéro de juge hors de la plage configurée pour cette confrontation.");

        var existant = confrontation.Votes.SingleOrDefault(v => v.JugeNumero == jugeNumero);
        if (existant != null)
        {
            existant.VoteCouleur = couleur;
            return existant;
        }

        var vote = new VoteJuge { ConfrontationId = confrontation.Id, JugeNumero = jugeNumero, VoteCouleur = couleur };
        confrontation.Votes.Add(vote);
        if (confrontation.Statut == StatutCombat.EnAttente) confrontation.Statut = StatutCombat.EnCours;
        return vote;
    }

    /// <summary>
    /// Tranche la confrontation une fois tous les juges votés : le compétiteur totalisant le plus
    /// de drapeaux gagne (cahier 5.5). Lève si des votes manquent, ou en cas d'égalité (nombre de
    /// juges pair) — à trancher manuellement par l'organisateur, l'application ne devine pas.
    /// </summary>
    public static void ValiderResultat(KataConfrontation confrontation)
    {
        if (confrontation.Votes.Count < confrontation.NbJuges)
            throw new InvalidOperationException($"Il manque {confrontation.NbJuges - confrontation.Votes.Count} vote(s) de juge.");

        var votesAka = confrontation.Votes.Count(v => v.VoteCouleur == Couleur.Aka);
        var votesAo = confrontation.Votes.Count - votesAka;
        if (votesAka == votesAo)
            throw new InvalidOperationException($"Égalité de drapeaux ({votesAka}-{votesAo}) — nombre de juges pair, à trancher manuellement.");

        confrontation.VainqueurCouleur = votesAka > votesAo ? Couleur.Aka : Couleur.Ao;
        confrontation.Statut = StatutCombat.Termine;
    }
}
