using FkcScoring.Core.Data;
using FkcScoring.Core.Data.Entities;
using Microsoft.EntityFrameworkCore;

namespace FkcScoring.Core.Domain;

/// <summary>
/// Génère et fait vivre les tableaux Kata : même moteur de format/tirage/arbre et de repêchage à
/// deux médailles de bronze que TableauService pour le Kumite (cahier 5.5 : « le tableau Kata suit
/// les mêmes règles de format automatique que le Kumite »). Le compétiteur (participant ou équipe,
/// selon la discipline de la catégorie) est routé vers Participant1/2Id ou Equipe1/2Id.
/// </summary>
public class KataTableauService
{
    private readonly FkcScoringContext _db;

    public KataTableauService(FkcScoringContext db) => _db = db;

    public Tableau GenererTableau(int categorieId, List<int> competiteurIds, Discipline discipline, int nbJuges, TableauFormatSeuils? seuils = null, int? seed = null)
    {
        if (competiteurIds.Distinct().Count() != competiteurIds.Count)
            throw new ArgumentException("Un même compétiteur ne peut pas apparaître deux fois dans le tirage.");

        var format = TableauFormatRules.DeterminerFormat(competiteurIds.Count, seuils);
        var tableau = new Tableau { CategorieId = categorieId, Format = format };
        _db.Tableaux.Add(tableau);
        _db.SaveChanges();

        var rng = seed.HasValue ? new Random(seed.Value) : new Random();
        var tires = competiteurIds.OrderBy(_ => rng.Next()).ToList();

        switch (format)
        {
            case FormatTableau.FinaleDirecte:
                CreerConfrontation(tableau.Id, tour: 1, moitie: null, c1: tires[0], c2: tires[1], discipline: discipline, nbJuges: nbJuges);
                break;
            case FormatTableau.PouleUnique:
                GenererPoule(tableau.Id, tires, discipline, nbJuges, estRepechage: false, moitie: null);
                break;
            case FormatTableau.PoulePuisElimination:
                GenererPoulesEquilibrees(tableau.Id, tires, discipline, nbJuges);
                break;
            case FormatTableau.EliminationRepechage:
                GenererArbreElimination(tableau.Id, tires, discipline, nbJuges, estRepechage: false, moitieUnique: null);
                break;
        }

        _db.SaveChanges();
        return tableau;
    }

    private void GenererPoule(int tableauId, List<int> ids, Discipline discipline, int nbJuges, bool estRepechage, int? moitie)
    {
        for (int i = 0; i < ids.Count; i++)
            for (int j = i + 1; j < ids.Count; j++)
                CreerConfrontation(tableauId, tour: 1, moitie: moitie, c1: ids[i], c2: ids[j], discipline: discipline, nbJuges: nbJuges, estRepechage: estRepechage);
    }

    /// <summary>Classement d'une poule par nombre de confrontations gagnées (cahier 5.5), à départage par différentiel de drapeaux.</summary>
    public List<(int CompetiteurId, int Victoires, int DifferenceDrapeaux)> ClasserPoule(IEnumerable<KataConfrontation> confrontationsPoule, Discipline discipline)
    {
        var stats = new Dictionary<int, (int V, int Diff)>();
        void Ajouter(int id, int diff, bool victoire)
        {
            stats.TryGetValue(id, out var cur);
            stats[id] = (cur.V + (victoire ? 1 : 0), cur.Diff + diff);
        }

        foreach (var c in confrontationsPoule.Where(c => c.Statut == StatutCombat.Termine))
        {
            var id1 = CompetiteurId(c, Couleur.Aka, discipline);
            var id2 = CompetiteurId(c, Couleur.Ao, discipline);
            if (id1 == null || id2 == null) continue;
            var vAka = c.Votes.Count(v => v.VoteCouleur == Couleur.Aka);
            var vAo = c.Votes.Count - vAka;
            Ajouter(id1.Value, vAka - vAo, c.VainqueurCouleur == Couleur.Aka);
            Ajouter(id2.Value, vAo - vAka, c.VainqueurCouleur == Couleur.Ao);
        }

        return stats
            .OrderByDescending(kv => kv.Value.V)
            .ThenByDescending(kv => kv.Value.Diff)
            .Select(kv => (kv.Key, kv.Value.V, kv.Value.Diff))
            .ToList();
    }

    private void GenererPoulesEquilibrees(int tableauId, List<int> tires, Discipline discipline, int nbJuges)
    {
        var poule1 = new List<int>();
        var poule2 = new List<int>();
        for (int i = 0; i < tires.Count; i++)
            (i % 2 == 0 ? poule1 : poule2).Add(tires[i]);

        GenererPoule(tableauId, poule1, discipline, nbJuges, estRepechage: false, moitie: 1);
        GenererPoule(tableauId, poule2, discipline, nbJuges, estRepechage: false, moitie: 2);
    }

    /// <summary>À appeler une fois toutes les poules d'un tableau PoulePuisElimination terminées.</summary>
    public void GenererPhaseEliminationApresPoules(int tableauId, Discipline discipline, int nbJuges)
    {
        var tableau = _db.Tableaux.Single(t => t.Id == tableauId);
        if (tableau.Format != FormatTableau.PoulePuisElimination)
            throw new InvalidOperationException("Ce tableau n'est pas au format poule(s) puis élimination.");

        var confrontationsPoules = _db.KataConfrontations.Include(c => c.Votes).Where(c => c.TableauId == tableauId).ToList();
        var qualifies = new List<int>();
        foreach (var moitie in new[] { 1, 2 })
        {
            var classement = ClasserPoule(confrontationsPoules.Where(c => c.Moitie == moitie), discipline);
            qualifies.AddRange(classement.Take(2).Select(c => c.CompetiteurId));
        }

        GenererArbreElimination(tableauId, qualifies, discipline, nbJuges, estRepechage: false, moitieUnique: null, tourDepart: 2);
    }

    private List<KataConfrontation> GenererArbreElimination(int tableauId, List<int> competiteurIds, Discipline discipline, int nbJuges, bool estRepechage, int? moitieUnique, int tourDepart = 1)
    {
        int n = competiteurIds.Count;
        int taille = 1;
        while (taille < n) taille *= 2;
        int nbByes = taille - n;
        int nbPaires = taille / 2;

        var idx = 0;
        var premierTour = new List<KataConfrontation>();
        for (int paire = 0; paire < nbPaires; paire++)
        {
            int? c1 = competiteurIds[idx++];
            int? c2 = paire < nbByes ? null : competiteurIds[idx++];

            int? moitie = estRepechage ? moitieUnique : (paire < nbPaires / 2 ? 1 : 2);
            if (!estRepechage && nbPaires == 1) moitie = moitieUnique;

            var confrontation = CreerConfrontation(tableauId, tour: tourDepart, moitie: moitie, c1: c1, c2: c2, discipline: discipline, nbJuges: nbJuges, estRepechage: estRepechage);
            if (c2 == null)
            {
                confrontation.EstBye = true;
                confrontation.Statut = StatutCombat.Termine;
                confrontation.VainqueurCouleur = Couleur.Aka;
            }
            premierTour.Add(confrontation);
        }
        _db.SaveChanges();

        var tourCourant = premierTour;
        int tour = tourDepart;
        while (tourCourant.Count > 1)
        {
            var tourSuivant = new List<KataConfrontation>();
            for (int i = 0; i < tourCourant.Count; i += 2)
            {
                var g1 = tourCourant[i];
                var g2 = tourCourant[i + 1];
                int? moitieSuivante = g1.Moitie == g2.Moitie ? g1.Moitie : null;

                var suivant = new KataConfrontation { TableauId = tableauId, Tour = tour + 1, Moitie = moitieSuivante, EstRepechage = estRepechage, Statut = StatutCombat.EnAttente, NbJuges = nbJuges };
                _db.KataConfrontations.Add(suivant);
                _db.SaveChanges();

                g1.ProchainConfrontationId = suivant.Id;
                g1.ProchainConfrontationCouleur = Couleur.Aka;
                g2.ProchainConfrontationId = suivant.Id;
                g2.ProchainConfrontationCouleur = Couleur.Ao;

                if (g1.EstBye) SetCompetiteur(suivant, Couleur.Aka, GetVainqueurId(g1, discipline), discipline);
                if (g2.EstBye) SetCompetiteur(suivant, Couleur.Ao, GetVainqueurId(g2, discipline), discipline);

                tourSuivant.Add(suivant);
            }
            _db.SaveChanges();
            tourCourant = tourSuivant;
            tour++;
        }

        return premierTour;
    }

    /// <summary>
    /// Enregistre le résultat d'une confrontation déjà décidée (KataEngine) : propage le vainqueur
    /// vers le tour suivant, et déclenche la génération du mini-tableau de repêchage si cette
    /// confrontation était la demi-finale d'une moitié du tableau principal.
    /// </summary>
    public void EnregistrerResultat(KataConfrontation confrontation, Discipline discipline)
    {
        if (confrontation.Statut != StatutCombat.Termine || confrontation.VainqueurCouleur == null)
            throw new InvalidOperationException("La confrontation doit être terminée et avoir un vainqueur avant d'être enregistrée.");

        _db.SaveChanges();

        if (confrontation.ProchainConfrontationId != null)
        {
            var suivant = _db.KataConfrontations.Single(c => c.Id == confrontation.ProchainConfrontationId);
            var vainqueurId = GetVainqueurId(confrontation, discipline);
            if (confrontation.ProchainConfrontationCouleur == Couleur.Aka) SetCompetiteur(suivant, Couleur.Aka, vainqueurId, discipline);
            else SetCompetiteur(suivant, Couleur.Ao, vainqueurId, discipline);
        }

        if (!confrontation.EstRepechage) TraiterFinDemiFinale(confrontation, discipline);

        _db.SaveChanges();
    }

    private void TraiterFinDemiFinale(KataConfrontation demiFinale, Discipline discipline)
    {
        var format = _db.Tableaux.Where(t => t.Id == demiFinale.TableauId).Select(t => t.Format).Single();
        if (format != FormatTableau.EliminationRepechage) return;

        if (demiFinale.Moitie == null) return; // c'est la finale (fusion des deux moitiés), pas une demi-finale
        var prochain = demiFinale.ProchainConfrontationId == null ? null : _db.KataConfrontations.Find(demiFinale.ProchainConfrontationId);
        if (prochain == null || prochain.Moitie != null) return; // ne mène pas directement à la finale

        var finalisteId = GetVainqueurId(demiFinale, discipline);
        if (finalisteId == null) return;

        var battus = _db.KataConfrontations
            .Where(c => c.TableauId == demiFinale.TableauId && c.Moitie == demiFinale.Moitie && !c.EstRepechage && c.Statut == StatutCombat.Termine)
            .ToList()
            .Where(c => GetVainqueurId(c, discipline) == finalisteId)
            .Select(c => c.VainqueurCouleur == Couleur.Aka ? CompetiteurId(c, Couleur.Ao, discipline) : CompetiteurId(c, Couleur.Aka, discipline))
            .Where(id => id != null)
            .Select(id => id!.Value)
            .Distinct()
            .ToList();

        if (battus.Count == 0) return; // ne devrait pas arriver (la demi-finale elle-même compte toujours)

        if (battus.Count == 1)
        {
            // Un seul adversaire écarté par le finaliste sur cette moitié : bronze direct, sans confrontation.
            var bronze = new KataConfrontation
            {
                TableauId = demiFinale.TableauId,
                Tour = demiFinale.Tour + 1,
                Moitie = demiFinale.Moitie,
                EstRepechage = true,
                EstBye = true,
                Statut = StatutCombat.Termine,
                VainqueurCouleur = Couleur.Aka,
                NbJuges = demiFinale.NbJuges
            };
            SetCompetiteur(bronze, Couleur.Aka, battus[0], discipline);
            _db.KataConfrontations.Add(bronze);
        }
        else
        {
            GenererArbreElimination(demiFinale.TableauId, battus, discipline, demiFinale.NbJuges, estRepechage: true, moitieUnique: demiFinale.Moitie);
        }
    }

    private KataConfrontation CreerConfrontation(int tableauId, int tour, int? moitie, int? c1, int? c2, Discipline discipline, int nbJuges, bool estRepechage = false)
    {
        var confrontation = new KataConfrontation
        {
            TableauId = tableauId,
            Tour = tour,
            Moitie = moitie,
            EstRepechage = estRepechage,
            Statut = StatutCombat.EnAttente,
            NbJuges = nbJuges
        };
        SetCompetiteur(confrontation, Couleur.Aka, c1, discipline);
        SetCompetiteur(confrontation, Couleur.Ao, c2, discipline);
        _db.KataConfrontations.Add(confrontation);
        _db.SaveChanges();
        return confrontation;
    }

    public static int? CompetiteurId(KataConfrontation c, Couleur couleur, Discipline discipline)
    {
        bool equipe = discipline == Discipline.KataEquipe;
        return couleur switch
        {
            Couleur.Aka => equipe ? c.Equipe1Id : c.Participant1Id,
            Couleur.Ao => equipe ? c.Equipe2Id : c.Participant2Id,
            _ => null
        };
    }

    public static void SetCompetiteur(KataConfrontation c, Couleur couleur, int? id, Discipline discipline)
    {
        bool equipe = discipline == Discipline.KataEquipe;
        if (couleur == Couleur.Aka)
        {
            if (equipe) c.Equipe1Id = id; else c.Participant1Id = id;
        }
        else
        {
            if (equipe) c.Equipe2Id = id; else c.Participant2Id = id;
        }
    }

    private static int? GetVainqueurId(KataConfrontation c, Discipline discipline) =>
        c.VainqueurCouleur == null ? null : CompetiteurId(c, c.VainqueurCouleur.Value, discipline);
}
