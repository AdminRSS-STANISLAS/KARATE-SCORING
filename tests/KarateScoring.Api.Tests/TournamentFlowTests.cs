using System.Net;
using System.Net.Http.Json;

namespace KarateScoring.Api.Tests;

/// <summary>
/// Exerce l'API réelle par HTTP (routage, validation des modèles, sérialisation JSON, persistance
/// SQLite) plutôt que la logique métier isolée — déjà couverte par FkcScoring.Core.Tests. Couvre le
/// scénario minimal exigé par le cahier §27 (Scoring : ajout score/pénalité/fin combat/victoire ;
/// Tournoi : création/catégories/tirage/tableau/progression).
/// </summary>
/// <summary>Chaque méthode de test xUnit reçoit une nouvelle instance de la classe (comportement par
/// défaut), donc créer l'<see cref="ApiFactory"/> ici plutôt que via <c>IClassFixture</c> donne à chaque
/// test sa propre base isolée — important pour <see cref="SecurityAndBackupSmokeTests"/> notamment, où
/// un code administrateur défini par un test ne doit pas fuiter vers les suivants.</summary>
public class TournamentFlowTests : IDisposable
{
    private readonly ApiFactory _factory = new();
    private readonly HttpClient _client;

    public TournamentFlowTests() => _client = _factory.CreateClient();
    public void Dispose() => _factory.Dispose();

    [Fact]
    public async Task Setup_CreatesCompetitionCategoryAndParticipants()
    {
        var comp = await CreerCompetition("Coupe Test HTTP");
        Assert.True(comp!.Id > 0);

        var cat = await _client.PostAsJsonAsync($"/api/competitions/{comp.Id}/categories",
            new CreateCategorieRequest("Seniors -75kg", "KumiteIndividuel", "Mixte", 18, 99, null));
        cat.EnsureSuccessStatusCode();
        var catDto = await cat.Content.ReadFromJsonAsync<CategorieDto>();
        Assert.Equal(0, catDto!.InscritsCount);

        var (p1, _) = await CreerEtInscrireParticipant("Awa", "Coulibaly", "Club A", catDto.Id);
        Assert.True(p1.Id > 0);

        var categories = await _client.GetFromJsonAsync<List<CategorieDto>>($"/api/competitions/{comp.Id}/categories");
        Assert.Equal(1, categories!.Single(c => c.Id == catDto.Id).InscritsCount);
    }

    [Fact]
    public async Task Combat_EndsAutomatically_OnPointGap_AndUpdatesClassement()
    {
        var comp = await CreerCompetition("Écart de points HTTP");
        var catDto = await CreerCategorie(comp!.Id, "Finale directe");
        var (p1, _) = await CreerEtInscrireParticipant("Junior", "Mbarga", "FKC", catDto.Id);
        var (p2, _) = await CreerEtInscrireParticipant("Awa", "Etoundi", "Douala", catDto.Id);

        var genRes = await _client.PostAsJsonAsync($"/api/categories/{catDto.Id}/tableau/generer", new GenererTableauRequest("FinaleDirecte"));
        genRes.EnsureSuccessStatusCode();
        var tableau = await genRes.Content.ReadFromJsonAsync<TableauDto>();
        var combat = Assert.Single(tableau!.Confrontations);
        Assert.Equal("EnAttente", combat.Statut);

        (await _client.PostAsync($"/api/combats/{combat.Id}/demarrer", null)).EnsureSuccessStatusCode();

        // Barème par défaut : Ippon = 3 pts, écart de victoire = 8 — trois Ippon (9 pts) suffisent à
        // déclencher la fin automatique du combat (cahier §10/16 : aucune saisie manuelle supplémentaire).
        ConfrontationDto? dernier = null;
        for (var i = 0; i < 3; i++)
        {
            var res = await _client.PostAsJsonAsync($"/api/combats/{combat.Id}/point", new PointRequest("Aka", "ippon", 10 + i * 5));
            res.EnsureSuccessStatusCode();
            dernier = await res.Content.ReadFromJsonAsync<ConfrontationDto>();
        }

        Assert.Equal("Termine", dernier!.Statut);
        Assert.Equal("Aka", dernier.VainqueurCouleur);
        Assert.Equal(9, dernier.ScoreAka);
        Assert.Equal("EcartPoints", dernier.ModeDecision);
        Assert.Equal(3, dernier.Evenements!.Count(e => e.Kind == "point"));

        var classement = await _client.GetFromJsonAsync<List<ClassementDto>>($"/api/categories/{catDto.Id}/classement");
        var vainqueur = p1.Id == dernier.AId ? p1 : p2;
        Assert.Equal($"{vainqueur.Prenom} {vainqueur.Nom}", classement!.First(c => c.Position == 1).Nom);
    }

    [Fact]
    public async Task Penalite_Disqualifiante_TermineLeCombatEtDesigneLeVainqueur()
    {
        var comp = await CreerCompetition("Disqualification HTTP");
        var catDto = await CreerCategorie(comp!.Id, "Finale directe");
        await CreerEtInscrireParticipant("Paul", "Ngono", "Club X", catDto.Id);
        await CreerEtInscrireParticipant("Léa", "Bikoro", "Club Y", catDto.Id);

        var tableau = await (await _client.PostAsJsonAsync($"/api/categories/{catDto.Id}/tableau/generer", new GenererTableauRequest("FinaleDirecte")))
            .Content.ReadFromJsonAsync<TableauDto>();
        var combat = tableau!.Confrontations.Single();
        (await _client.PostAsync($"/api/combats/{combat.Id}/demarrer", null)).EnsureSuccessStatusCode();

        var res = await _client.PostAsJsonAsync($"/api/combats/{combat.Id}/penalite", new PenaliteRequest("Ao", "Hansoku", 20));
        res.EnsureSuccessStatusCode();
        var apres = await res.Content.ReadFromJsonAsync<ConfrontationDto>();

        Assert.Equal("Termine", apres!.Statut);
        Assert.Equal("Aka", apres.VainqueurCouleur);
        Assert.Equal("Disqualification", apres.ModeDecision);
    }

    [Fact]
    public async Task GenererTableau_AvecMoinsDeDeuxInscrits_Renvoie400()
    {
        var comp = await CreerCompetition("Validation HTTP");
        var catDto = await CreerCategorie(comp!.Id, "Un seul inscrit");
        await CreerEtInscrireParticipant("Solo", "Athlete", "Club Seul", catDto.Id);

        var res = await _client.PostAsJsonAsync($"/api/categories/{catDto.Id}/tableau/generer", new GenererTableauRequest(null));
        Assert.Equal(HttpStatusCode.BadRequest, res.StatusCode);
    }

    private async Task<CompetitionDto?> CreerCompetition(string nom)
    {
        var res = await _client.PostAsJsonAsync("/api/competitions", new CreateCompetitionRequest(nom, DateTime.Today, "Gymnase Test", "Club"));
        res.EnsureSuccessStatusCode();
        return await res.Content.ReadFromJsonAsync<CompetitionDto>();
    }

    private async Task<CategorieDto> CreerCategorie(int competitionId, string nom)
    {
        var res = await _client.PostAsJsonAsync($"/api/competitions/{competitionId}/categories",
            new CreateCategorieRequest(nom, "KumiteIndividuel", "Mixte", 18, 99, null));
        res.EnsureSuccessStatusCode();
        return (await res.Content.ReadFromJsonAsync<CategorieDto>())!;
    }

    private async Task<(ParticipantDto, int)> CreerEtInscrireParticipant(string prenom, string nom, string club, int categorieId)
    {
        var res = await _client.PostAsJsonAsync("/api/participants", new CreateParticipantRequest(nom, prenom, club, null, new DateTime(2000, 1, 1), null, null));
        res.EnsureSuccessStatusCode();
        var participant = (await res.Content.ReadFromJsonAsync<ParticipantDto>())!;
        (await _client.PutAsJsonAsync($"/api/participants/{participant.Id}/inscriptions", new InscriptionsRequest(new List<int> { categorieId }))).EnsureSuccessStatusCode();
        return (participant, categorieId);
    }
}
