using FkcScoring.Core.Data;
using FkcScoring.Core.Data.Entities;

namespace FkcScoring.Core.Domain;

/// <summary>
/// Génère et fait vivre les tableaux de compétition : détermination automatique du format selon
/// l'effectif (cahier 5.3), tirage au sort, arbre à élimination directe et repêchage à deux
/// médailles de bronze (peuplé dynamiquement dès qu'une demi-finale de moitié est connue, sans
/// attendre l'issue de la finale).
/// </summary>
public class TableauService
{
    private readonly FkcScoringContext _db;

    public TableauService(FkcScoringContext db) => _db = db;

    public Tableau GenererTableau(int categorieId, List<int> participantIds, TableauFormatSeuils? seuils = null, int? seed = null)
    {
        if (participantIds.Distinct().Count() != participantIds.Count)
            throw new ArgumentException("Un même participant ne peut pas apparaître deux fois dans le tirage.");

        var format = TableauFormatRules.DeterminerFormat(participantIds.Count, seuils);
        var tableau = new Tableau { CategorieId = categorieId, Format = format };
        _db.Tableaux.Add(tableau);
        _db.SaveChanges();

        var rng = seed.HasValue ? new Random(seed.Value) : new Random();
        var tires = participantIds.OrderBy(_ => rng.Next()).ToList();

        switch (format)
        {
            case FormatTableau.FinaleDirecte:
                CreerCombat(tableau.Id, tour: 1, moitie: null, akaId: tires[0], aoId: tires[1]);
                break;

            case FormatTableau.PouleUnique:
                GenererPoule(tableau.Id, tires, estRepechage: false, moitie: null);
                break;

            case FormatTableau.PoulePuisElimination:
                GenererPoulesEquilibrees(tableau.Id, tires);
                break;

            case FormatTableau.EliminationRepechage:
                GenererArbreElimination(tableau.Id, tires, estRepechage: false, moitieUnique: null);
                break;
        }

        _db.SaveChanges();
        return tableau;
    }

    // ---- Poule unique (3-4) : tous contre tous, un seul groupe ----
    private void GenererPoule(int tableauId, List<int> participantIds, bool estRepechage, int? moitie)
    {
        for (int i = 0; i < participantIds.Count; i++)
            for (int j = i + 1; j < participantIds.Count; j++)
                CreerCombat(tableauId, tour: 1, moitie: moitie, akaId: participantIds[i], aoId: participantIds[j], estRepechage: estRepechage);
    }

    /// <summary>Classement d'une poule par nombre de victoires (cahier 5.3), à départage par différence de points.</summary>
    public List<(int ParticipantId, int Victoires, int DifferencePoints)> ClasserPoule(IEnumerable<Combat> combatsPoule)
    {
        var stats = new Dictionary<int, (int V, int Diff)>();
        void Ajouter(int id, int diff, bool victoire)
        {
            stats.TryGetValue(id, out var cur);
            stats[id] = (cur.V + (victoire ? 1 : 0), cur.Diff + diff);
        }

        foreach (var c in combatsPoule.Where(c => c.Statut == StatutCombat.Termine && c.CompetiteurAkaId != null && c.CompetiteurAoId != null))
        {
            Ajouter(c.CompetiteurAkaId!.Value, c.ScoreAka - c.ScoreAo, c.VainqueurCouleur == Couleur.Aka);
            Ajouter(c.CompetiteurAoId!.Value, c.ScoreAo - c.ScoreAka, c.VainqueurCouleur == Couleur.Ao);
        }

        return stats
            .OrderByDescending(kv => kv.Value.V)
            .ThenByDescending(kv => kv.Value.Diff)
            .Select(kv => (kv.Key, kv.Value.V, kv.Value.Diff))
            .ToList();
    }

    // ---- Poule(s) puis élimination (5-7) : deux poules équilibrées, phase 1 seulement ----
    // La phase d'élimination (top 2 de chaque poule) est générée séparément une fois les poules
    // terminées, via GenererPhaseEliminationApresPoules — les qualifiés ne sont pas connus au tirage.
    private void GenererPoulesEquilibrees(int tableauId, List<int> tires)
    {
        var poule1 = new List<int>();
        var poule2 = new List<int>();
        for (int i = 0; i < tires.Count; i++)
            (i % 2 == 0 ? poule1 : poule2).Add(tires[i]);

        GenererPoule(tableauId, poule1, estRepechage: false, moitie: 1);
        GenererPoule(tableauId, poule2, estRepechage: false, moitie: 2);
    }

    /// <summary>À appeler une fois toutes les poules d'un tableau PoulePuisElimination terminées.</summary>
    public void GenererPhaseEliminationApresPoules(int tableauId)
    {
        var tableau = _db.Tableaux.Single(t => t.Id == tableauId);
        if (tableau.Format != FormatTableau.PoulePuisElimination)
            throw new InvalidOperationException("Ce tableau n'est pas au format poule(s) puis élimination.");

        var combatsPoules = _db.Combats.Where(c => c.TableauId == tableauId).ToList();
        var qualifies = new List<int>();
        foreach (var moitie in new[] { 1, 2 })
        {
            var classement = ClasserPoule(combatsPoules.Where(c => c.Moitie == moitie));
            qualifies.AddRange(classement.Take(2).Select(c => c.ParticipantId));
        }

        GenererArbreElimination(tableauId, qualifies, estRepechage: false, moitieUnique: null, tourDepart: 2);
    }

    // ---- Élimination directe (avec ou sans repêchage) ----
    private List<Combat> GenererArbreElimination(int tableauId, List<int> participantIds, bool estRepechage, int? moitieUnique, int tourDepart = 1)
    {
        int n = participantIds.Count;
        int taille = 1;
        while (taille < n) taille *= 2;
        int nbByes = taille - n;
        int nbPaires = taille / 2;

        var idx = 0;
        var premierTour = new List<Combat>();
        for (int paire = 0; paire < nbPaires; paire++)
        {
            int? aka = participantIds[idx++];
            int? ao = paire < nbByes ? null : participantIds[idx++];

            int? moitie = estRepechage ? moitieUnique : (paire < nbPaires / 2 ? 1 : 2);
            // taille=2 (n=2 au repêchage) : nbPaires=1, pas de notion de moitié à subdiviser davantage
            if (!estRepechage && nbPaires == 1) moitie = moitieUnique;

            var combat = CreerCombat(tableauId, tour: tourDepart, moitie: moitie, akaId: aka, aoId: ao, estRepechage: estRepechage);
            if (ao == null)
            {
                combat.EstBye = true;
                combat.Statut = StatutCombat.Termine;
                combat.VainqueurCouleur = Couleur.Aka;
            }
            premierTour.Add(combat);
        }
        _db.SaveChanges();

        // Construit les tours suivants (placeholders), en propageant immédiatement les exemptions (byes).
        var tourCourant = premierTour;
        int tour = tourDepart;
        while (tourCourant.Count > 1)
        {
            var tourSuivant = new List<Combat>();
            for (int i = 0; i < tourCourant.Count; i += 2)
            {
                var c1 = tourCourant[i];
                var c2 = tourCourant[i + 1];
                int? moitieSuivante = c1.Moitie == c2.Moitie ? c1.Moitie : null;

                var suivant = new Combat { TableauId = tableauId, Tour = tour + 1, Moitie = moitieSuivante, EstRepechage = estRepechage, Statut = StatutCombat.EnAttente };
                _db.Combats.Add(suivant);
                _db.SaveChanges();

                c1.ProchainCombatId = suivant.Id;
                c1.ProchainCombatCouleur = Couleur.Aka;
                c2.ProchainCombatId = suivant.Id;
                c2.ProchainCombatCouleur = Couleur.Ao;

                if (c1.EstBye) suivant.CompetiteurAkaId = c1.VainqueurParticipantId;
                if (c2.EstBye) suivant.CompetiteurAoId = c2.VainqueurParticipantId;

                tourSuivant.Add(suivant);
            }
            _db.SaveChanges();
            tourCourant = tourSuivant;
            tour++;
        }

        return premierTour;
    }

    /// <summary>
    /// Enregistre le résultat d'un combat déjà décidé (CombatEngine) : propage le vainqueur vers le
    /// tour suivant, et déclenche la génération du mini-tableau de repêchage si ce combat était la
    /// demi-finale d'une moitié du tableau principal.
    /// </summary>
    public void EnregistrerResultatCombat(Combat combat)
    {
        if (combat.Statut != StatutCombat.Termine || combat.VainqueurCouleur == null)
            throw new InvalidOperationException("Le combat doit être terminé et avoir un vainqueur avant d'être enregistré.");

        // Persiste l'état "Terminé" avant les requêtes de propagation/repêchage ci-dessous, pour ne
        // jamais raisonner sur une ligne encore périmée côté base.
        _db.SaveChanges();

        if (combat.ProchainCombatId != null)
        {
            var suivant = _db.Combats.Single(c => c.Id == combat.ProchainCombatId);
            if (combat.ProchainCombatCouleur == Couleur.Aka) suivant.CompetiteurAkaId = combat.VainqueurParticipantId;
            else suivant.CompetiteurAoId = combat.VainqueurParticipantId;
        }

        if (!combat.EstRepechage) TraiterFinDemiFinale(combat);

        _db.SaveChanges();
    }

    private void TraiterFinDemiFinale(Combat demiFinale)
    {
        // Le repêchage à deux bronzes ne s'applique qu'au format élimination directe (8+ participants,
        // cahier 5.3) : la moitié de tableau posée sur les poules d'un format "poule(s) puis élimination"
        // (5-7) a un sens différent (groupe de poule) et ne doit jamais déclencher ce mécanisme.
        var format = _db.Tableaux.Where(t => t.Id == demiFinale.TableauId).Select(t => t.Format).Single();
        if (format != FormatTableau.EliminationRepechage) return;

        if (demiFinale.Moitie == null) return; // c'est la finale (fusion des deux moitiés), pas une demi-finale
        var prochain = demiFinale.ProchainCombatId == null ? null : _db.Combats.Find(demiFinale.ProchainCombatId);
        if (prochain == null || prochain.Moitie != null) return; // ne mène pas directement à la finale

        var finalisteId = demiFinale.VainqueurParticipantId;
        if (finalisteId == null) return;

        var battus = _db.Combats
            .Where(c => c.TableauId == demiFinale.TableauId && c.Moitie == demiFinale.Moitie && !c.EstRepechage && c.Statut == StatutCombat.Termine)
            .ToList()
            .Where(c => c.VainqueurParticipantId == finalisteId)
            .Select(c => c.VainqueurCouleur == Couleur.Aka ? c.CompetiteurAoId : c.CompetiteurAkaId)
            .Where(id => id != null)
            .Select(id => id!.Value)
            .Distinct()
            .ToList();

        if (battus.Count == 0) return; // ne devrait pas arriver (la demi-finale elle-même compte toujours)

        if (battus.Count == 1)
        {
            // Un seul adversaire écarté par le finaliste sur cette moitié : bronze direct, sans combat.
            var bronze = new Combat
            {
                TableauId = demiFinale.TableauId,
                Tour = demiFinale.Tour + 1,
                Moitie = demiFinale.Moitie,
                EstRepechage = true,
                EstBye = true,
                Statut = StatutCombat.Termine,
                CompetiteurAkaId = battus[0],
                VainqueurCouleur = Couleur.Aka
            };
            _db.Combats.Add(bronze);
        }
        else
        {
            GenererArbreElimination(demiFinale.TableauId, battus, estRepechage: true, moitieUnique: demiFinale.Moitie);
        }
    }

    private Combat CreerCombat(int tableauId, int tour, int? moitie, int? akaId, int? aoId, bool estRepechage = false)
    {
        var combat = new Combat
        {
            TableauId = tableauId,
            Tour = tour,
            Moitie = moitie,
            CompetiteurAkaId = akaId,
            CompetiteurAoId = aoId,
            EstRepechage = estRepechage,
            Statut = StatutCombat.EnAttente
        };
        _db.Combats.Add(combat);
        _db.SaveChanges();
        return combat;
    }
}
