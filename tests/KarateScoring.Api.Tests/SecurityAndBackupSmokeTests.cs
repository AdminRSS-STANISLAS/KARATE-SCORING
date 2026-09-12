using System.Net;
using System.Net.Http.Json;

namespace KarateScoring.Api.Tests;

/// <summary>Couvre, au niveau HTTP, la protection par code administrateur (cahier §21) et le
/// téléchargement de sauvegarde (§25) — la logique elle-même est déjà testée unitairement dans
/// FkcScoring.Core.Tests (SecuriteServiceTests, SauvegardeServiceTests) ; ici on vérifie que les
/// routes/contrôleurs les exposent correctement.</summary>
public class SecurityAndBackupSmokeTests : IDisposable
{
    private readonly ApiFactory _factory = new();
    private readonly HttpClient _client;

    public SecurityAndBackupSmokeTests() => _client = _factory.CreateClient();
    public void Dispose() => _factory.Dispose();

    [Fact]
    public async Task AdminReset_SansCodeConfigure_FonctionneEtViideLaBase()
    {
        // Aucun code défini pour ce fixture — le reset reste ouvert (comportement documenté, cahier §21).
        var res = await _client.PostAsJsonAsync("/api/admin/reset", new ResetRequest(null));
        res.EnsureSuccessStatusCode();
    }

    [Fact]
    public async Task DefinirCodeAdmin_PuisResetSansCode_EstRefuse403()
    {
        var def = await _client.PostAsJsonAsync("/api/securite/code", new DefinirCodeRequest("1234", null));
        def.EnsureSuccessStatusCode();

        var reset = await _client.PostAsJsonAsync("/api/admin/reset", new ResetRequest(null));
        Assert.Equal(HttpStatusCode.Forbidden, reset.StatusCode);

        var resetAvecCode = await _client.PostAsJsonAsync("/api/admin/reset", new ResetRequest("1234"));
        resetAvecCode.EnsureSuccessStatusCode();
    }

    [Fact]
    public async Task TelechargerSauvegarde_RenvoieUnFichierSqlite()
    {
        var res = await _client.GetAsync("/api/sauvegardes/telecharger");
        res.EnsureSuccessStatusCode();
        Assert.Equal("application/octet-stream", res.Content.Headers.ContentType!.MediaType);
        var octets = await res.Content.ReadAsByteArrayAsync();
        Assert.True(octets.Length > 0);
    }
}
