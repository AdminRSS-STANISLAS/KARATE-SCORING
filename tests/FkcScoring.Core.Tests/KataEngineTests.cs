using FkcScoring.Core.Data.Entities;
using FkcScoring.Core.Domain;
using Xunit;

namespace FkcScoring.Core.Tests;

public class KataEngineTests
{
    private static KataConfrontation NouvelleConfrontation(int nbJuges = 5) => new()
    {
        Id = 1,
        Participant1Id = 1,
        Participant2Id = 2,
        NbJuges = nbJuges,
        Statut = StatutCombat.EnAttente
    };

    [Fact]
    public void DefinirKata_AffecteLeKataDeChaqueCouleur()
    {
        var confrontation = NouvelleConfrontation();
        KataEngine.DefinirKata(confrontation, Couleur.Aka, kataId: 3);
        KataEngine.DefinirKata(confrontation, Couleur.Ao, kataId: 9);

        Assert.Equal(3, confrontation.Kata1Id);
        Assert.Equal(9, confrontation.Kata2Id);
    }

    [Fact]
    public void EnregistrerVote_AjouteLeVoteEtPasseEnCours()
    {
        var confrontation = NouvelleConfrontation();
        KataEngine.EnregistrerVote(confrontation, jugeNumero: 1, Couleur.Aka);

        Assert.Single(confrontation.Votes);
        Assert.Equal(StatutCombat.EnCours, confrontation.Statut);
    }

    [Fact]
    public void EnregistrerVote_MemeJugeDeuxFois_RemplaceLeVotePrecedent()
    {
        var confrontation = NouvelleConfrontation();
        KataEngine.EnregistrerVote(confrontation, jugeNumero: 1, Couleur.Aka);
        KataEngine.EnregistrerVote(confrontation, jugeNumero: 1, Couleur.Ao);

        Assert.Single(confrontation.Votes);
        Assert.Equal(Couleur.Ao, confrontation.Votes[0].VoteCouleur);
    }

    [Fact]
    public void EnregistrerVote_NumeroDeJugeHorsPlage_Leve()
    {
        var confrontation = NouvelleConfrontation(nbJuges: 5);
        Assert.Throws<ArgumentOutOfRangeException>(() => KataEngine.EnregistrerVote(confrontation, jugeNumero: 6, Couleur.Aka));
    }

    [Fact]
    public void ValiderResultat_VotesIncomplets_Leve()
    {
        var confrontation = NouvelleConfrontation(nbJuges: 5);
        KataEngine.EnregistrerVote(confrontation, 1, Couleur.Aka);
        Assert.Throws<InvalidOperationException>(() => KataEngine.ValiderResultat(confrontation));
    }

    [Fact]
    public void ValiderResultat_MajoriteDeDrapeaux_DesigneLeVainqueur()
    {
        var confrontation = NouvelleConfrontation(nbJuges: 5);
        KataEngine.EnregistrerVote(confrontation, 1, Couleur.Aka);
        KataEngine.EnregistrerVote(confrontation, 2, Couleur.Aka);
        KataEngine.EnregistrerVote(confrontation, 3, Couleur.Aka);
        KataEngine.EnregistrerVote(confrontation, 4, Couleur.Ao);
        KataEngine.EnregistrerVote(confrontation, 5, Couleur.Ao);

        KataEngine.ValiderResultat(confrontation);

        Assert.Equal(Couleur.Aka, confrontation.VainqueurCouleur);
        Assert.Equal(StatutCombat.Termine, confrontation.Statut);
    }

    [Fact]
    public void ValiderResultat_EgaliteDeDrapeaux_Leve()
    {
        var confrontation = NouvelleConfrontation(nbJuges: 4);
        KataEngine.EnregistrerVote(confrontation, 1, Couleur.Aka);
        KataEngine.EnregistrerVote(confrontation, 2, Couleur.Aka);
        KataEngine.EnregistrerVote(confrontation, 3, Couleur.Ao);
        KataEngine.EnregistrerVote(confrontation, 4, Couleur.Ao);

        var ex = Assert.Throws<InvalidOperationException>(() => KataEngine.ValiderResultat(confrontation));
        Assert.Contains("Égalité", ex.Message);
        Assert.Null(confrontation.VainqueurCouleur);
    }
}
