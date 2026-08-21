using FkcScoring.Core.Data;
using FkcScoring.Core.Data.Entities;
using FkcScoring.Core.Domain;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using Xunit;

namespace FkcScoring.Core.Tests;

public class KataTableauServiceTests : IDisposable
{
    private readonly SqliteConnection _connection;
    private readonly FkcScoringContext _db;
    private readonly KataTableauService _service;
    private readonly int _categorieIndividuelId;
    private readonly int _categorieEquipeId;

    public KataTableauServiceTests()
    {
        _connection = new SqliteConnection("DataSource=:memory:");
        _connection.Open();
        var options = new DbContextOptionsBuilder<FkcScoringContext>().UseSqlite(_connection).Options;
        _db = new FkcScoringContext(options);
        _db.Database.EnsureCreated();
        _service = new KataTableauService(_db);

        var club = new Club { Nom = "FKC" };
        _db.Clubs.Add(club);
        _db.SaveChanges();
        for (int i = 1; i <= 20; i++)
            _db.Participants.Add(new Participant { Nom = $"Nom{i}", Prenom = $"Prenom{i}", ClubId = club.Id });
        var competition = new Competition { Nom = "Test", Date = DateTime.Today };
        _db.Competitions.Add(competition);
        _db.SaveChanges();

        var catIndividuel = new Categorie { CompetitionId = competition.Id, Nom = "Kata Individuel", Discipline = Discipline.KataIndividuel };
        var catEquipe = new Categorie { CompetitionId = competition.Id, Nom = "Kata Équipe", Discipline = Discipline.KataEquipe };
        _db.Categories.AddRange(catIndividuel, catEquipe);
        _db.SaveChanges();
        _categorieIndividuelId = catIndividuel.Id;
        _categorieEquipeId = catEquipe.Id;

        for (int i = 1; i <= 6; i++)
            _db.Equipes.Add(new Equipe { CategorieId = catEquipe.Id, Nom = $"Equipe{i}", ClubId = club.Id });
        _db.SaveChanges();
    }

    public void Dispose()
    {
        _db.Dispose();
        _connection.Dispose();
    }

    private List<int> ParticipantIds(int n) => _db.Participants.OrderBy(p => p.Id).Take(n).Select(p => p.Id).ToList();
    private List<int> EquipeIds(int n) => _db.Equipes.OrderBy(e => e.Id).Take(n).Select(e => e.Id).ToList();

    [Fact]
    public void GenererTableau_DeuxCompetiteurs_CreeUneFinaleDirecte()
    {
        var tableau = _service.GenererTableau(_categorieIndividuelId, ParticipantIds(2), Discipline.KataIndividuel, nbJuges: 5);
        Assert.Equal(FormatTableau.FinaleDirecte, tableau.Format);
        var confrontations = _db.KataConfrontations.Where(c => c.TableauId == tableau.Id).ToList();
        Assert.Single(confrontations);
        Assert.NotNull(confrontations[0].Participant1Id);
        Assert.NotNull(confrontations[0].Participant2Id);
        Assert.Equal(5, confrontations[0].NbJuges);
    }

    [Fact]
    public void GenererTableau_DisciplineEquipe_RouteVersLesChampsEquipe()
    {
        var tableau = _service.GenererTableau(_categorieEquipeId, EquipeIds(2), Discipline.KataEquipe, nbJuges: 7);
        var confrontation = _db.KataConfrontations.Single(c => c.TableauId == tableau.Id);

        Assert.NotNull(confrontation.Equipe1Id);
        Assert.NotNull(confrontation.Equipe2Id);
        Assert.Null(confrontation.Participant1Id);
        Assert.Null(confrontation.Participant2Id);
    }

    [Fact]
    public void GenererTableau_QuatreCompetiteurs_CreeUnePouleTousContreTous()
    {
        var tableau = _service.GenererTableau(_categorieIndividuelId, ParticipantIds(4), Discipline.KataIndividuel, nbJuges: 5);
        Assert.Equal(FormatTableau.PouleUnique, tableau.Format);
        var confrontations = _db.KataConfrontations.Where(c => c.TableauId == tableau.Id).ToList();
        Assert.Equal(6, confrontations.Count); // C(4,2) = 6
    }

    [Fact]
    public void GenererTableau_HuitCompetiteurs_CreeUnArbreAvecDemiFinalesEtFinale()
    {
        var tableau = _service.GenererTableau(_categorieIndividuelId, ParticipantIds(8), Discipline.KataIndividuel, nbJuges: 5);
        Assert.Equal(FormatTableau.EliminationRepechage, tableau.Format);

        var confrontations = _db.KataConfrontations.Where(c => c.TableauId == tableau.Id && !c.EstRepechage).ToList();
        Assert.Equal(4, confrontations.Count(c => c.Tour == 1));
        Assert.Equal(2, confrontations.Count(c => c.Tour == 2));
        Assert.Equal(1, confrontations.Count(c => c.Tour == 3));

        var finale = confrontations.Single(c => c.Tour == 3);
        Assert.Null(finale.Moitie);
    }

    [Fact]
    public void TableauAHuit_DerouleEntierement_ProduitDeuxBronzesEtUnePasFinaliste()
    {
        var ids = ParticipantIds(8);
        var tableau = _service.GenererTableau(_categorieIndividuelId, ids, Discipline.KataIndividuel, nbJuges: 5, seed: 42);

        for (int garde = 0; garde < 20; garde++)
        {
            var jouables = _db.KataConfrontations
                .Where(c => c.TableauId == tableau.Id && c.Statut == StatutCombat.EnAttente
                    && c.Participant1Id != null && c.Participant2Id != null)
                .ToList();
            if (jouables.Count == 0) break;

            foreach (var confrontation in jouables)
            {
                confrontation.Statut = StatutCombat.Termine;
                confrontation.VainqueurCouleur = Couleur.Aka; // Aka gagne systématiquement, résultat déterministe
                _service.EnregistrerResultat(confrontation, Discipline.KataIndividuel);
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

    [Fact]
    public void GenererTableau_SixCompetiteurs_CreeDeuxPoulesPuisPhaseElimination()
    {
        var tableau = _service.GenererTableau(_categorieIndividuelId, ParticipantIds(6), Discipline.KataIndividuel, nbJuges: 5, seed: 1);
        Assert.Equal(FormatTableau.PoulePuisElimination, tableau.Format);

        var poules = _db.KataConfrontations.Where(c => c.TableauId == tableau.Id).ToList();
        Assert.Equal(6, poules.Count); // 2 poules de 3 -> C(3,2)=3 chacune

        foreach (var c in poules)
        {
            c.Statut = StatutCombat.Termine;
            c.VainqueurCouleur = Couleur.Aka;
        }
        _db.SaveChanges();

        _service.GenererPhaseEliminationApresPoules(tableau.Id, Discipline.KataIndividuel, nbJuges: 5);
        var phaseElim = _db.KataConfrontations.Where(c => c.TableauId == tableau.Id && c.Tour >= 2).ToList();
        Assert.Equal(2, phaseElim.Count(c => c.Tour == 2));
        Assert.Equal(1, phaseElim.Count(c => c.Tour == 3));
    }
}
