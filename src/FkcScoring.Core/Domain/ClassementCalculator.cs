using FkcScoring.Core.Data;
using FkcScoring.Core.Data.Entities;

namespace FkcScoring.Core.Domain;

/// <summary>Calcule et enregistre le classement final d'une catégorie une fois son tableau terminé.</summary>
public class ClassementCalculator
{
    private readonly FkcScoringContext _db;

    public ClassementCalculator(FkcScoringContext db) => _db = db;

    public List<Classement> CalculerEtEnregistrer(int tableauId)
    {
        var tableau = _db.Tableaux.Single(t => t.Id == tableauId);
        var combats = _db.Combats.Where(c => c.TableauId == tableauId).ToList();
        var resultats = new List<Classement>();

        switch (tableau.Format)
        {
            case FormatTableau.FinaleDirecte:
                AjouterFinaleDirecte(resultats, tableau, combats);
                break;
            case FormatTableau.PouleUnique:
                AjouterPouleUnique(resultats, tableau, combats);
                break;
            case FormatTableau.PoulePuisElimination:
            case FormatTableau.EliminationRepechage:
                AjouterElimination(resultats, tableau, combats);
                break;
        }

        _db.Classements.RemoveRange(_db.Classements.Where(c => c.CategorieId == tableau.CategorieId));
        _db.Classements.AddRange(resultats);
        _db.SaveChanges();
        return resultats;
    }

    private static void AjouterFinaleDirecte(List<Classement> resultats, Tableau tableau, List<Combat> combats)
    {
        var finale = combats.Single();
        if (finale.VainqueurCouleur == null) return;
        var perdantId = finale.VainqueurCouleur == Couleur.Aka ? finale.CompetiteurAoId : finale.CompetiteurAkaId;
        resultats.Add(new Classement { CategorieId = tableau.CategorieId, ParticipantId = finale.VainqueurParticipantId, Position = 1, Medaille = Medaille.Or });
        if (perdantId != null)
            resultats.Add(new Classement { CategorieId = tableau.CategorieId, ParticipantId = perdantId, Position = 2, Medaille = Medaille.Argent });
    }

    private void AjouterPouleUnique(List<Classement> resultats, Tableau tableau, List<Combat> combats)
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

    private static void AjouterElimination(List<Classement> resultats, Tableau tableau, List<Combat> combats)
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
}
