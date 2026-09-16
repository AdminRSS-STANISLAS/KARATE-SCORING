using System.Net.Http.Json;

namespace KarateScoring.Api.Tests;

/// <summary>
/// Couvre les exports PDF/Excel bout en bout — jamais exercés par un test avant cette session, ce qui
/// a laissé passer un vrai bug en production : QuestPDF exige depuis ses versions récentes une
/// auto-déclaration de licence au démarrage, jamais configurée, donc les deux exports PDF échouaient
/// systématiquement avec une 500. Trouvé en simulant une compétition complète niveau club et en testant
/// les boutons d'export, pas par un test automatisé — d'où ce test, pour que ça ne puisse plus repasser
/// inaperçu.
/// </summary>
public class ExportTests : IDisposable, IAsyncLifetime
{
    private readonly ApiFactory _factory = new();
    private readonly HttpClient _client;

    public ExportTests() => _client = _factory.CreateClient();
    public Task InitializeAsync() => _factory.ActiverLicenceAsync(_client);
    public Task DisposeAsync() => Task.CompletedTask;
    public void Dispose() => _factory.Dispose();

    [Fact]
    public async Task ExportKumite_UneFoisUnCombatTermine_RenvoieUnPdfValide()
    {
        var (comp, cat) = await PreparerCompetitionAvecCombatTermine();

        var res = await _client.GetAsync($"/api/competitions/{comp.Id}/export/kumite");
        res.EnsureSuccessStatusCode();
        Assert.Equal("application/pdf", res.Content.Headers.ContentType!.MediaType);
        var octets = await res.Content.ReadAsByteArrayAsync();
        Assert.True(octets.Length > 100);
        Assert.Equal("%PDF"u8.ToArray(), octets[..4]);
    }

    [Fact]
    public async Task ExportKata_AvecConfrontationTerminee_RenvoieUnPdfValide()
    {
        var comp = (await (await _client.PostAsJsonAsync("/api/competitions",
            new CreateCompetitionRequest("Export Kata Test", DateTime.Today, "Gymnase", "Club")))
            .Content.ReadFromJsonAsync<CompetitionDto>())!;
        var cat = (await (await _client.PostAsJsonAsync($"/api/competitions/{comp.Id}/categories",
            new CreateCategorieRequest("Kata Test", "KataIndividuel", "Mixte", 18, 99, null)))
            .Content.ReadFromJsonAsync<CategorieDto>())!;
        foreach (var (prenom, nom) in new[] { ("A", "Un"), ("B", "Deux") })
        {
            var p = (await (await _client.PostAsJsonAsync("/api/participants",
                new CreateParticipantRequest(nom, prenom, "Club " + nom, null, new DateTime(2000, 1, 1), null, null)))
                .Content.ReadFromJsonAsync<ParticipantDto>())!;
            (await _client.PutAsJsonAsync($"/api/participants/{p.Id}/inscriptions", new InscriptionsRequest(new List<int> { cat.Id }))).EnsureSuccessStatusCode();
        }
        var tableau = (await (await _client.PostAsJsonAsync($"/api/categories/{cat.Id}/tableau/generer", new GenererTableauRequest("FinaleDirecte")))
            .Content.ReadFromJsonAsync<TableauDto>())!;
        var conf = tableau.Confrontations.Single();
        var kata = (await (await _client.PostAsJsonAsync("/api/katas", new CreateKataRequest("Heian Shodan")))
            .Content.ReadFromJsonAsync<KataDto>())!;
        (await _client.PostAsJsonAsync($"/api/kata-confrontations/{conf.Id}/kata", new DefinirKataRequest("Aka", kata.Id))).EnsureSuccessStatusCode();
        (await _client.PostAsJsonAsync($"/api/kata-confrontations/{conf.Id}/kata", new DefinirKataRequest("Ao", kata.Id))).EnsureSuccessStatusCode();
        for (var j = 1; j <= 5; j++)
            (await _client.PostAsJsonAsync($"/api/kata-confrontations/{conf.Id}/vote", new VoteRequest(j, j <= 3 ? "Aka" : "Ao"))).EnsureSuccessStatusCode();
        (await _client.PostAsync($"/api/kata-confrontations/{conf.Id}/valider", null)).EnsureSuccessStatusCode();

        var res = await _client.GetAsync($"/api/competitions/{comp.Id}/export/kata");
        res.EnsureSuccessStatusCode();
        var octets = await res.Content.ReadAsByteArrayAsync();
        Assert.Equal("%PDF"u8.ToArray(), octets[..4]);
    }

    [Fact]
    public async Task ExportExcel_RenvoieUnClasseurValide()
    {
        var (comp, _) = await PreparerCompetitionAvecCombatTermine();

        var res = await _client.GetAsync($"/api/competitions/{comp.Id}/export/excel");
        res.EnsureSuccessStatusCode();
        var octets = await res.Content.ReadAsByteArrayAsync();
        Assert.Equal("PK"u8.ToArray(), octets[..2]); // .xlsx est une archive zip
    }

    private async Task<(CompetitionDto Comp, CategorieDto Cat)> PreparerCompetitionAvecCombatTermine()
    {
        var comp = (await (await _client.PostAsJsonAsync("/api/competitions",
            new CreateCompetitionRequest("Export Kumite Test", DateTime.Today, "Gymnase", "Club")))
            .Content.ReadFromJsonAsync<CompetitionDto>())!;
        var cat = (await (await _client.PostAsJsonAsync($"/api/competitions/{comp.Id}/categories",
            new CreateCategorieRequest("Kumite Test", "KumiteIndividuel", "Mixte", 18, 99, null)))
            .Content.ReadFromJsonAsync<CategorieDto>())!;
        foreach (var (prenom, nom) in new[] { ("A", "Un"), ("B", "Deux") })
        {
            var p = (await (await _client.PostAsJsonAsync("/api/participants",
                new CreateParticipantRequest(nom, prenom, "Club " + nom, null, new DateTime(2000, 1, 1), null, null)))
                .Content.ReadFromJsonAsync<ParticipantDto>())!;
            (await _client.PutAsJsonAsync($"/api/participants/{p.Id}/inscriptions", new InscriptionsRequest(new List<int> { cat.Id }))).EnsureSuccessStatusCode();
        }
        var tableau = (await (await _client.PostAsJsonAsync($"/api/categories/{cat.Id}/tableau/generer", new GenererTableauRequest("FinaleDirecte")))
            .Content.ReadFromJsonAsync<TableauDto>())!;
        var combat = tableau.Confrontations.Single();
        (await _client.PostAsync($"/api/combats/{combat.Id}/demarrer", null)).EnsureSuccessStatusCode();
        for (var i = 0; i < 3; i++)
            (await _client.PostAsJsonAsync($"/api/combats/{combat.Id}/point", new PointRequest("Aka", "ippon", 10))).EnsureSuccessStatusCode();
        return (comp, cat);
    }
}
