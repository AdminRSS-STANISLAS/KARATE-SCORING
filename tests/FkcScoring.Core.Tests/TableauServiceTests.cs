using FkcScoring.Core.Data;
using FkcScoring.Core.Data.Entities;
using FkcScoring.Core.Domain;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using Xunit;

namespace FkcScoring.Core.Tests;

public class TableauServiceTests : IDisposable
{
    private readonly SqliteConnection _connection;
    private readonly FkcScoringContext _db;
    private readonly TableauService _service;

    public TableauServiceTests()
    {
        _connection = new SqliteConnection("DataSource=:memory:");
        _connection.Open();
        var options = new DbContextOptionsBuilder<FkcScoringContext>().UseSqlite(_connection).Options;
        _db = new FkcScoringContext(options);
        _db.Database.EnsureCreated();
        _service = new TableauService(_db);

        var club = new Club { Nom = "FKC" };
        _db.Clubs.Add(club);
        _db.SaveChanges();
        for (int i = 1; i <= 20; i++)
            _db.Participants.Add(new Participant { Nom = $"Nom{i}", Prenom = $"Prenom{i}", ClubId = club.Id });
        var competition = new Competition { Nom = "Test", Date = DateTime.Today };
        _db.Competitions.Add(competition);
        _db.SaveChanges();
        _db.Categories.Add(new Categorie { CompetitionId = competition.Id, Nom = "Cat", Discipline = Discipline.KumiteIndividuel });
        _db.SaveChanges();
    }

    public void Dispose()
    {
        _db.Dispose();
        _connection.Dispose();
    }

    private List<int> ParticipantIds(int n) => _db.Participants.OrderBy(p => p.Id).Take(n).Select(p => p.Id).ToList();
    private int CategorieId => _db.Categories.Select(c => c.Id).First();

    [Fact]
    public void GenererTableau_DeuxParticipants_CreeUneFinaleDirecte()
    {
        var tableau = _service.GenererTableau(CategorieId, ParticipantIds(2));
        Assert.Equal(FormatTableau.FinaleDirecte, tableau.Format);
        var combats = _db.Combats.Where(c => c.TableauId == tableau.Id).ToList();
        Assert.Single(combats);
        Assert.NotNull(combats[0].CompetiteurAkaId);
        Assert.NotNull(combats[0].CompetiteurAoId);
    }

    [Fact]
    public void GenererTableau_QuatreParticipants_CreeUnePouleTousContreTous()
    {
        var tableau = _service.GenererTableau(CategorieId, ParticipantIds(4));
        Assert.Equal(FormatTableau.PouleUnique, tableau.Format);
        var combats = _db.Combats.Where(c => c.TableauId == tableau.Id).ToList();
        Assert.Equal(6, combats.Count); // C(4,2) = 6
    }

    [Fact]
    public void GenererTableau_HuitParticipants_CreeUnArbreAvecDemiFinalesEtFinale()
    {
        var tableau = _service.GenererTableau(CategorieId, ParticipantIds(8));
        Assert.Equal(FormatTableau.EliminationRepechage, tableau.Format);

        var combats = _db.Combats.Where(c => c.TableauId == tableau.Id && !c.EstRepechage).ToList();
        Assert.Equal(4, combats.Count(c => c.Tour == 1)); // quarts
        Assert.Equal(2, combats.Count(c => c.Tour == 2)); // demies
        Assert.Equal(1, combats.Count(c => c.Tour == 3)); // finale

        var finale = combats.Single(c => c.Tour == 3);
        Assert.Null(finale.Moitie);
        foreach (var demi in combats.Where(c => c.Tour == 2))
            Assert.NotNull(demi.Moitie);
    }

    [Fact]
    public void TableauAHuit_DerouleEntierement_ProduitDeuxBronzesEtUnePasFinaliste()
    {
        var ids = ParticipantIds(8);
        var tableau = _service.GenererTableau(CategorieId, ids, seed: 42);

        // Déroule tous les tours (y compris repêchage) jusqu'à ce que plus aucun combat ne soit jouable.
        for (int garde = 0; garde < 20; garde++)
        {
            var jouable = _db.Combats
                .Where(c => c.TableauId == tableau.Id && c.Statut == StatutCombat.EnAttente
                    && c.CompetiteurAkaId != null && c.CompetiteurAoId != null)
                .ToList();
            if (jouable.Count == 0) break;

            foreach (var combat in jouable)
            {
                combat.Statut = StatutCombat.Termine;
                combat.VainqueurCouleur = Couleur.Aka; // Aka gagne systématiquement, résultat déterministe
                _service.EnregistrerResultatCombat(combat);
            }
        }

        var calculator = new ClassementCalculator(_db);
        var classement = calculator.CalculerEtEnregistrer(tableau.Id);

        Assert.Contains(classement, c => c.Medaille == Medaille.Or);
        Assert.Contains(classement, c => c.Medaille == Medaille.Argent);
        Assert.Contains(classement, c => c.Medaille == Medaille.Bronze1);
        Assert.Contains(classement, c => c.Medaille == Medaille.Bronze2);

        // Personne ne doit recevoir deux médailles.
        var participantsMedailles = classement.Select(c => c.ParticipantId).ToList();
        Assert.Equal(participantsMedailles.Count, participantsMedailles.Distinct().Count());
    }

    [Fact]
    public void GenererTableau_SixParticipants_CreeDeuxPoulesPuisPhaseElimination()
    {
        var tableau = _service.GenererTableau(CategorieId, ParticipantIds(6), seed: 1);
        Assert.Equal(FormatTableau.PoulePuisElimination, tableau.Format);

        var poules = _db.Combats.Where(c => c.TableauId == tableau.Id).ToList();
        Assert.Equal(6, poules.Count); // 2 poules de 3 -> C(3,2)=3 chacune

        foreach (var c in poules)
        {
            c.Statut = StatutCombat.Termine;
            c.ScoreAka = 3; c.ScoreAo = 0;
            c.VainqueurCouleur = Couleur.Aka;
        }
        _db.SaveChanges();

        _service.GenererPhaseEliminationApresPoules(tableau.Id);
        var phaseElim = _db.Combats.Where(c => c.TableauId == tableau.Id && c.Tour >= 2).ToList();
        Assert.Equal(2, phaseElim.Count(c => c.Tour == 2)); // demies
        Assert.Equal(1, phaseElim.Count(c => c.Tour == 3)); // finale
    }

    [Fact]
    public void GenererPhaseEliminationApresPoules_CroiseLesPoules_EviteUnRematchEnDemiFinale()
    {
        var tableau = _service.GenererTableau(CategorieId, ParticipantIds(6), seed: 1);
        var poules = _db.Combats.Where(c => c.TableauId == tableau.Id).ToList();

        (int Premier, int Deuxieme) ClasserEtJouer(int moitie)
        {
            var combatsMoitie = poules.Where(c => c.Moitie == moitie).ToList();
            var ids = combatsMoitie.SelectMany(c => new[] { c.CompetiteurAkaId!.Value, c.CompetiteurAoId!.Value }).Distinct().ToList();
            foreach (var c in combatsMoitie)
            {
                c.Statut = StatutCombat.Termine;
                c.VainqueurCouleur = ids.IndexOf(c.CompetiteurAkaId!.Value) < ids.IndexOf(c.CompetiteurAoId!.Value) ? Couleur.Aka : Couleur.Ao;
            }
            return (ids[0], ids[1]);
        }

        var (p1, p2) = ClasserEtJouer(1);
        var (q1, q2) = ClasserEtJouer(2);
        _db.SaveChanges();

        _service.GenererPhaseEliminationApresPoules(tableau.Id);

        var demies = _db.Combats.Where(c => c.TableauId == tableau.Id && c.Tour == 2).ToList();
        foreach (var demi in demies)
        {
            var paire = new[] { demi.CompetiteurAkaId, demi.CompetiteurAoId };
            Assert.False(paire.Contains(p1) && paire.Contains(p2), "Le 1er et le 2e de la même poule ne doivent pas se rencontrer en demi-finale.");
            Assert.False(paire.Contains(q1) && paire.Contains(q2), "Le 1er et le 2e de la même poule ne doivent pas se rencontrer en demi-finale.");
        }
    }

    [Fact]
    public void TableauASix_DerouleEntierement_ProduitDeuxBronzes()
    {
        var tableau = _service.GenererTableau(CategorieId, ParticipantIds(6), seed: 1);
        var poules = _db.Combats.Where(c => c.TableauId == tableau.Id).ToList();
        foreach (var c in poules)
        {
            c.Statut = StatutCombat.Termine;
            c.ScoreAka = 3; c.ScoreAo = 0;
            c.VainqueurCouleur = Couleur.Aka;
        }
        _db.SaveChanges();

        _service.GenererPhaseEliminationApresPoules(tableau.Id);

        for (int garde = 0; garde < 10; garde++)
        {
            var jouable = _db.Combats
                .Where(c => c.TableauId == tableau.Id && c.Statut == StatutCombat.EnAttente
                    && c.CompetiteurAkaId != null && c.CompetiteurAoId != null)
                .ToList();
            if (jouable.Count == 0) break;

            foreach (var combat in jouable)
            {
                combat.Statut = StatutCombat.Termine;
                combat.VainqueurCouleur = Couleur.Aka;
                _service.EnregistrerResultatCombat(combat);
            }
        }

        var calculator = new ClassementCalculator(_db);
        var classement = calculator.CalculerEtEnregistrer(tableau.Id);

        Assert.Contains(classement, c => c.Medaille == Medaille.Or);
        Assert.Contains(classement, c => c.Medaille == Medaille.Argent);
        Assert.Contains(classement, c => c.Medaille == Medaille.Bronze1);
        Assert.Contains(classement, c => c.Medaille == Medaille.Bronze2);

        var participantsMedailles = classement.Select(c => c.ParticipantId).ToList();
        Assert.Equal(participantsMedailles.Count, participantsMedailles.Distinct().Count());
    }
}
