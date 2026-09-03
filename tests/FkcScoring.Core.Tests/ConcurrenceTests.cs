using FkcScoring.Core.Data;
using FkcScoring.Core.Data.Entities;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using Xunit;

namespace FkcScoring.Core.Tests;

/// <summary>
/// Vérifie le jeton de concurrence (RowVersion, Phase 2) : deux postes qui chargent le même combat
/// puis écrivent tous les deux doivent produire un conflit détectable côté serveur, pas un écrasement
/// silencieux du premier par le second.
/// </summary>
public class ConcurrenceTests : IDisposable
{
    private readonly SqliteConnection _connection;

    public ConcurrenceTests()
    {
        _connection = new SqliteConnection("DataSource=:memory:");
        _connection.Open();
        using var setup = NouveauContexte();
        setup.Database.EnsureCreated();

        var club = new Club { Nom = "FKC" };
        setup.Clubs.Add(club);
        setup.SaveChanges();
        setup.Participants.Add(new Participant { Nom = "A", Prenom = "A", ClubId = club.Id });
        setup.Participants.Add(new Participant { Nom = "B", Prenom = "B", ClubId = club.Id });
        var competition = new Competition { Nom = "Test", Date = DateTime.Today };
        setup.Competitions.Add(competition);
        setup.SaveChanges();
        var categorie = new Categorie { CompetitionId = competition.Id, Nom = "Cat", Discipline = Discipline.KumiteIndividuel };
        setup.Categories.Add(categorie);
        setup.SaveChanges();
        var tableau = new Tableau { CategorieId = categorie.Id, Format = FormatTableau.FinaleDirecte };
        setup.Tableaux.Add(tableau);
        setup.SaveChanges();
        setup.Combats.Add(new Combat { TableauId = tableau.Id, Tour = 1, CompetiteurAkaId = 1, CompetiteurAoId = 2, Statut = StatutCombat.EnCours });
        setup.SaveChanges();
    }

    public void Dispose() => _connection.Dispose();

    private FkcScoringContext NouveauContexte()
    {
        var options = new DbContextOptionsBuilder<FkcScoringContext>().UseSqlite(_connection).Options;
        return new FkcScoringContext(options);
    }

    [Fact]
    public void DeuxPostesModifientLeMemeCombat_LeSecondEnregistrementEchoueAuLieuDEcraserLePremier()
    {
        using var posteArbitre = NouveauContexte();
        using var posteControle = NouveauContexte();

        var combatVuParArbitre = posteArbitre.Combats.Single();
        var combatVuParControle = posteControle.Combats.Single();

        combatVuParArbitre.ScoreAka = 2;
        posteArbitre.SaveChanges(); // premier à écrire : passe, RowVersion incrémenté

        combatVuParControle.ScoreAka = 3; // travaille encore sur l'ancien RowVersion chargé au début
        Assert.Throws<DbUpdateConcurrencyException>(() => posteControle.SaveChanges());
    }
}
