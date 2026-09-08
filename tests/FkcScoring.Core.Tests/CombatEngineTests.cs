using FkcScoring.Core.Data.Entities;
using FkcScoring.Core.Domain;
using Xunit;

namespace FkcScoring.Core.Tests;

public class CombatEngineTests
{
    private static Combat NouveauCombatEnCours() => new()
    {
        Id = 1,
        CompetiteurAkaId = 1,
        CompetiteurAoId = 2,
        Statut = StatutCombat.EnCours
    };

    [Fact]
    public void AjouterPoint_IncrementeLeScoreEtPoseLeSenshu()
    {
        var combat = NouveauCombatEnCours();
        CombatEngine.AjouterPoint(combat, Couleur.Aka, 2, TimeSpan.FromSeconds(10));

        Assert.Equal(2, combat.ScoreAka);
        Assert.Equal(Couleur.Aka, combat.SenshuCouleur);

        CombatEngine.AjouterPoint(combat, Couleur.Ao, 1, TimeSpan.FromSeconds(20));
        Assert.Equal(Couleur.Aka, combat.SenshuCouleur); // le Senshu reste au premier marqueur
    }

    [Fact]
    public void VerifierEcartVictoire_DeclencheAHuitPointsDecart()
    {
        var combat = NouveauCombatEnCours();
        CombatEngine.AjouterPoint(combat, Couleur.Aka, 3, TimeSpan.Zero);
        Assert.False(CombatEngine.VerifierEcartVictoire(combat, ecartVictoire: 8));

        combat.ScoreAka = 8;
        combat.ScoreAo = 0;
        Assert.True(CombatEngine.VerifierEcartVictoire(combat, ecartVictoire: 8));
        Assert.Equal(Couleur.Aka, combat.VainqueurCouleur);
        Assert.Equal(ModeDecision.EcartPoints, combat.ModeDecision);
        Assert.Equal(StatutCombat.Termine, combat.Statut);
    }

    [Fact]
    public void TerminerParFinDeTemps_MeilleurScoreGagne()
    {
        var combat = NouveauCombatEnCours();
        combat.ScoreAka = 3;
        combat.ScoreAo = 1;

        CombatEngine.TerminerParFinDeTemps(combat, estEnPoule: false);

        Assert.Equal(Couleur.Aka, combat.VainqueurCouleur);
        Assert.Equal(ModeDecision.FinTemps, combat.ModeDecision);
        Assert.Equal(StatutCombat.Termine, combat.Statut);
    }

    [Fact]
    public void TerminerParFinDeTemps_EgaliteEnPoule_TrancheeParLeSenshu()
    {
        var combat = NouveauCombatEnCours();
        combat.ScoreAka = 2;
        combat.ScoreAo = 2;
        combat.SenshuCouleur = Couleur.Ao;

        CombatEngine.TerminerParFinDeTemps(combat, estEnPoule: true);

        Assert.Equal(Couleur.Ao, combat.VainqueurCouleur);
        Assert.Equal(StatutCombat.Termine, combat.Statut);
    }

    [Fact]
    public void TerminerParFinDeTemps_EgaliteHorsPoule_BasculeSurHantei()
    {
        var combat = NouveauCombatEnCours();
        combat.ScoreAka = 0;
        combat.ScoreAo = 0;

        CombatEngine.TerminerParFinDeTemps(combat, estEnPoule: false);

        Assert.Null(combat.VainqueurCouleur);
        Assert.Equal(ModeDecision.Hantei, combat.ModeDecision);
        Assert.Equal(StatutCombat.EnCours, combat.Statut); // en attente du vote arbitral

        CombatEngine.EnregistrerHantei(combat, Couleur.Aka);
        Assert.Equal(Couleur.Aka, combat.VainqueurCouleur);
        Assert.Equal(StatutCombat.Termine, combat.Statut);
    }

    [Fact]
    public void EnregistrerHantei_RefuseUnCombatQuiNAttendPasDeHantei()
    {
        var combat = NouveauCombatEnCours(); // ModeDecision est encore null, pas Hantei
        Assert.Throws<InvalidOperationException>(() => CombatEngine.EnregistrerHantei(combat, Couleur.Aka));
    }

    [Fact]
    public void EnregistrerHantei_RefuseUnCombatDejaTermine()
    {
        var combat = NouveauCombatEnCours();
        combat.ScoreAka = 0;
        combat.ScoreAo = 0;
        CombatEngine.TerminerParFinDeTemps(combat, estEnPoule: false); // pose ModeDecision = Hantei, Statut reste EnCours
        CombatEngine.EnregistrerHantei(combat, Couleur.Aka); // premier vote : légitime, termine le combat

        Assert.Throws<InvalidOperationException>(() => CombatEngine.EnregistrerHantei(combat, Couleur.Ao)); // second vote : refusé
    }

    [Theory]
    [InlineData(TypePenalite.Hansoku)]
    [InlineData(TypePenalite.Shikkaku)]
    [InlineData(TypePenalite.Kiken)]
    public void AppliquerPenalite_DisqualifianteMetFinAuCombat(TypePenalite penalite)
    {
        var combat = NouveauCombatEnCours();
        CombatEngine.AppliquerPenalite(combat, Couleur.Aka, penalite, TimeSpan.FromSeconds(30));

        Assert.Equal(Couleur.Ao, combat.VainqueurCouleur);
        Assert.Equal(ModeDecision.Disqualification, combat.ModeDecision);
        Assert.Equal(StatutCombat.Termine, combat.Statut);
    }

    [Fact]
    public void AppliquerPenalite_Chukoku_NeTerminePasLeCombat()
    {
        var combat = NouveauCombatEnCours();
        CombatEngine.AppliquerPenalite(combat, Couleur.Aka, TypePenalite.Chukoku, TimeSpan.FromSeconds(5));

        Assert.Null(combat.VainqueurCouleur);
        Assert.Equal(StatutCombat.EnCours, combat.Statut);
        Assert.Single(combat.Evenements);
    }
}
