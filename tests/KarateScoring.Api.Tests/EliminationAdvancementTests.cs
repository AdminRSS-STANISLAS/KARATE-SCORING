using System.Net.Http.Json;

namespace KarateScoring.Api.Tests;

/// <summary>
/// Vérifie, par HTTP, le comportement central d'un tableau à élimination directe : le vainqueur de
/// chaque combat avance automatiquement au combat suivant, avec une couleur Aka/Ao ré-attribuée pour
/// ce nouveau combat (pas simplement reportée) — exactement le comportement que l'utilisateur a demandé
/// de vérifier pour l'affichage du tableau à crochets.
/// </summary>
public class EliminationAdvancementTests : IDisposable
{
    private readonly ApiFactory _factory = new();
    private readonly HttpClient _client;

    public EliminationAdvancementTests() => _client = _factory.CreateClient();
    public void Dispose() => _factory.Dispose();

    [Fact]
    public async Task Vainqueurs_DesDemiFinales_AvancentEnFinale_AvecCouleurReattribuee()
    {
        var comp = (await (await _client.PostAsJsonAsync("/api/competitions",
            new CreateCompetitionRequest("Bracket HTTP", DateTime.Today, "Gymnase", "Club")))
            .Content.ReadFromJsonAsync<CompetitionDto>())!;

        var cat = (await (await _client.PostAsJsonAsync($"/api/competitions/{comp.Id}/categories",
            new CreateCategorieRequest("4 inscrits", "KumiteIndividuel", "Mixte", 18, 99, null)))
            .Content.ReadFromJsonAsync<CategorieDto>())!;

        var noms = new[] { ("A", "Un"), ("B", "Deux"), ("C", "Trois"), ("D", "Quatre") };
        foreach (var (prenom, nom) in noms)
        {
            var p = (await (await _client.PostAsJsonAsync("/api/participants",
                new CreateParticipantRequest(nom, prenom, "Club " + nom, null, new DateTime(2000, 1, 1), null, null)))
                .Content.ReadFromJsonAsync<ParticipantDto>())!;
            (await _client.PutAsJsonAsync($"/api/participants/{p.Id}/inscriptions", new InscriptionsRequest(new List<int> { cat.Id }))).EnsureSuccessStatusCode();
        }

        var genRes = await _client.PostAsJsonAsync($"/api/categories/{cat.Id}/tableau/generer", new GenererTableauRequest("EliminationRepechage"));
        genRes.EnsureSuccessStatusCode();
        var tableau = (await genRes.Content.ReadFromJsonAsync<TableauDto>())!;

        var demiFinales = tableau.Confrontations.Where(c => c.Tour == 1 && !c.EstRepechage).ToList();
        Assert.Equal(2, demiFinales.Count);
        Assert.All(demiFinales, c => Assert.NotNull(c.AId));
        Assert.All(demiFinales, c => Assert.NotNull(c.BId));

        var vainqueurs = new List<int>();
        foreach (var demi in demiFinales)
        {
            (await _client.PostAsync($"/api/combats/{demi.Id}/demarrer", null)).EnsureSuccessStatusCode();
            ConfrontationDto? apres = null;
            for (var i = 0; i < 3; i++)
            {
                var res = await _client.PostAsJsonAsync($"/api/combats/{demi.Id}/point", new PointRequest("Aka", "ippon", 10 + i * 5));
                res.EnsureSuccessStatusCode();
                apres = await res.Content.ReadFromJsonAsync<ConfrontationDto>();
            }
            Assert.Equal("Termine", apres!.Statut);
            vainqueurs.Add(demi.AId!.Value); // couleur Aka a marqué les points ci-dessus, donc AId est bien le vainqueur
        }

        var tableauApres = await _client.GetFromJsonAsync<TableauDto>($"/api/categories/{cat.Id}/tableau");
        var finale = tableauApres!.Confrontations.Single(c => c.Tour == 2 && !c.EstRepechage);

        Assert.NotNull(finale.AId);
        Assert.NotNull(finale.BId);
        Assert.Equal("EnAttente", finale.Statut);
        Assert.Equal(vainqueurs.OrderBy(x => x), new[] { finale.AId!.Value, finale.BId!.Value }.OrderBy(x => x));
    }
}
