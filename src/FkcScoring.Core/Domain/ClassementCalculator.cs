using FkcScoring.Core.Data;
using FkcScoring.Core.Data.Entities;

namespace FkcScoring.Core.Domain;

/// <summary>Calcule et enregistre le classement final d'une catégorie une fois son tableau terminé (Kumite ou Kata).</summary>
public class ClassementCalculator
{
    private readonly FkcScoringContext _db;

    public ClassementCalculator(FkcScoringContext db) => _db = db;

    public List<Classement> CalculerEtEnregistrer(int tableauId)
    {
        var tableau = _db.Tableaux.Single(t => t.Id == tableauId);
        var categorie = _db.Categories.Single(c => c.Id == tableau.CategorieId);
        var resultats = categorie.Discipline == Discipline.KumiteIndividuel
            ? CalculerKumite(tableau)
            : CalculerKata(tableau, categorie.Discipline);

        _db.Classements.RemoveRange(_db.Classements.Where(c => c.CategorieId == tableau.CategorieId));
        _db.Classements.AddRange(resultats);
        _db.SaveChanges();
        return resultats;
    }

    // ==================== Kumite ====================

    private List<Classement> CalculerKumite(Tableau tableau)
    {
        var combats = _db.Combats.Where(c => c.TableauId == tableau.Id).ToList();
        var resultats = new List<Classement>();

        switch (tableau.Format)
        {
            case FormatTableau.FinaleDirecte:
                AjouterFinaleDirecteKumite(resultats, tableau, combats);
                break;
            case FormatTableau.PouleUnique:
                AjouterPouleUniqueKumite(resultats, tableau, combats);
                break;
            case FormatTableau.PoulePuisElimination:
            case FormatTableau.EliminationRepechage:
                AjouterEliminationKumite(resultats, tableau, combats);
                break;
        }

        return resultats;
    }

    private static void AjouterFinaleDirecteKumite(List<Classement> resultats, Tableau tableau, List<Combat> combats)
    {
        var finale = combats.Single();
        if (finale.VainqueurCouleur == null) return;
        var perdantId = finale.VainqueurCouleur == Couleur.Aka ? finale.CompetiteurAoId : finale.CompetiteurAkaId;
        resultats.Add(new Classement { CategorieId = tableau.CategorieId, ParticipantId = finale.VainqueurParticipantId, Position = 1, Medaille = Medaille.Or });
        if (perdantId != null)
            resultats.Add(new Classement { CategorieId = tableau.CategorieId, ParticipantId = perdantId, Position = 2, Medaille = Medaille.Argent });
    }

    private void AjouterPouleUniqueKumite(List<Classement> resultats, Tableau tableau, List<Combat> combats)
    {
        var service = new TableauService(_db);
        var classement = service.ClasserPoule(combats);
        var medailles = new[] { Medaille.Or, Medaille.Argent, Medaille.Bronze1, Medaille.Bronze2 };
        for (int i = 0; i < classement.Count; i++)
        {
            resultats.Add(new Classement
            {
                CategorieId = tableau.CategorieId,
                ParticipantId = classement[i].ParticipantId,
                Position = i + 1,
                Medaille = i < medailles.Length ? medailles[i] : null
            });
        }
    }

    private static void AjouterEliminationKumite(List<Classement> resultats, Tableau tableau, List<Combat> combats)
    {
        var finale = combats.SingleOrDefault(c => !c.EstRepechage && c.Moitie == null && c.ProchainCombatId == null);
        if (finale?.VainqueurCouleur != null)
        {
            var perdantId = finale.VainqueurCouleur == Couleur.Aka ? finale.CompetiteurAoId : finale.CompetiteurAkaId;
            resultats.Add(new Classement { CategorieId = tableau.CategorieId, ParticipantId = finale.VainqueurParticipantId, Position = 1, Medaille = Medaille.Or });
            if (perdantId != null)
                resultats.Add(new Classement { CategorieId = tableau.CategorieId, ParticipantId = perdantId, Position = 2, Medaille = Medaille.Argent });
        }

        foreach (var moitie in new[] { 1, 2 })
        {
            var bronze = combats
                .Where(c => c.EstRepechage && c.Moitie == moitie && c.ProchainCombatId == null && c.Statut == StatutCombat.Termine)
                .OrderByDescending(c => c.Tour)
                .FirstOrDefault();
            if (bronze?.VainqueurParticipantId != null)
            {
                resultats.Add(new Classement
                {
                    CategorieId = tableau.CategorieId,
                    ParticipantId = bronze.VainqueurParticipantId,
                    Position = 3,
                    Medaille = moitie == 1 ? Medaille.Bronze1 : Medaille.Bronze2
                });
            }
        }
    }

    // ==================== Kata ====================

    private List<Classement> CalculerKata(Tableau tableau, Discipline discipline)
    {
        var confrontations = _db.KataConfrontations.Where(c => c.TableauId == tableau.Id).ToList();
        foreach (var c in confrontations) _db.Entry(c).Collection(x => x.Votes).Load();
        var resultats = new List<Classement>();

        switch (tableau.Format)
        {
            case FormatTableau.FinaleDirecte:
                AjouterFinaleDirecteKata(resultats, tableau, confrontations, discipline);
                break;
            case FormatTableau.PouleUnique:
                AjouterPouleUniqueKata(resultats, tableau, confrontations, discipline);
                break;
            case FormatTableau.PoulePuisElimination:
            case FormatTableau.EliminationRepechage:
                AjouterEliminationKata(resultats, tableau, confrontations, discipline);
                break;
        }

        return resultats;
    }

    private static Classement NouveauClassementKata(int categorieId, int competiteurId, int position, Medaille? medaille, Discipline discipline) =>
        discipline == Discipline.KataEquipe
            ? new Classement { CategorieId = categorieId, EquipeId = competiteurId, Position = position, Medaille = medaille }
            : new Classement { CategorieId = categorieId, ParticipantId = competiteurId, Position = position, Medaille = medaille };

    private static void AjouterFinaleDirecteKata(List<Classement> resultats, Tableau tableau, List<KataConfrontation> confrontations, Discipline discipline)
    {
        var finale = confrontations.Single();
        if (finale.VainqueurCouleur == null) return;
        var vainqueurId = KataTableauService.CompetiteurId(finale, finale.VainqueurCouleur.Value, discipline);
        var perdantId = KataTableauService.CompetiteurId(finale, finale.VainqueurCouleur == Couleur.Aka ? Couleur.Ao : Couleur.Aka, discipline);
        if (vainqueurId != null) resultats.Add(NouveauClassementKata(tableau.CategorieId, vainqueurId.Value, 1, Medaille.Or, discipline));
        if (perdantId != null) resultats.Add(NouveauClassementKata(tableau.CategorieId, perdantId.Value, 2, Medaille.Argent, discipline));
    }

    private void AjouterPouleUniqueKata(List<Classement> resultats, Tableau tableau, List<KataConfrontation> confrontations, Discipline discipline)
    {
        var service = new KataTableauService(_db);
        var classement = service.ClasserPoule(confrontations, discipline);
        var medailles = new[] { Medaille.Or, Medaille.Argent, Medaille.Bronze1, Medaille.Bronze2 };
        for (int i = 0; i < classement.Count; i++)
        {
            resultats.Add(NouveauClassementKata(tableau.CategorieId, classement[i].CompetiteurId, i + 1, i < medailles.Length ? medailles[i] : null, discipline));
        }
    }

    private static void AjouterEliminationKata(List<Classement> resultats, Tableau tableau, List<KataConfrontation> confrontations, Discipline discipline)
    {
        var finale = confrontations.SingleOrDefault(c => !c.EstRepechage && c.Moitie == null && c.ProchainConfrontationId == null);
        if (finale?.VainqueurCouleur != null)
        {
            var vainqueurId = KataTableauService.CompetiteurId(finale, finale.VainqueurCouleur.Value, discipline);
            var perdantId = KataTableauService.CompetiteurId(finale, finale.VainqueurCouleur == Couleur.Aka ? Couleur.Ao : Couleur.Aka, discipline);
            if (vainqueurId != null) resultats.Add(NouveauClassementKata(tableau.CategorieId, vainqueurId.Value, 1, Medaille.Or, discipline));
            if (perdantId != null) resultats.Add(NouveauClassementKata(tableau.CategorieId, perdantId.Value, 2, Medaille.Argent, discipline));
        }

        foreach (var moitie in new[] { 1, 2 })
        {
            var bronze = confrontations
                .Where(c => c.EstRepechage && c.Moitie == moitie && c.ProchainConfrontationId == null && c.Statut == StatutCombat.Termine)
                .OrderByDescending(c => c.Tour)
                .FirstOrDefault();
            if (bronze?.VainqueurCouleur == null) continue;
            var bronzeId = KataTableauService.CompetiteurId(bronze, bronze.VainqueurCouleur.Value, discipline);
            if (bronzeId != null)
                resultats.Add(NouveauClassementKata(tableau.CategorieId, bronzeId.Value, 3, moitie == 1 ? Medaille.Bronze1 : Medaille.Bronze2, discipline));
        }
    }
}
