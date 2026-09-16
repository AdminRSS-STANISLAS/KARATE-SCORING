using FkcScoring.Core.Data;
using FkcScoring.Core.Domain;
using Microsoft.EntityFrameworkCore;

// QuestPDF (exports PDF résultats Kumite/Kata) exige une auto-déclaration de licence au démarrage
// depuis ses versions récentes, sans quoi toute génération de PDF échoue avec une 500 — jamais
// configuré jusqu'ici, donc les exports PDF étaient cassés en silence. Community : gratuit pour les
// particuliers/associations/organisations sous 1M$ de chiffre d'affaires annuel, le cas de ce projet.
QuestPDF.Settings.License = QuestPDF.Infrastructure.LicenseType.Community;

var builder = WebApplication.CreateBuilder(args);

// Écoute sur toutes les interfaces (pas juste localhost) pour que les postes de scoring des autres
// tatamis, sur le même réseau Wi-Fi/Ethernet local, puissent atteindre ce poste central — aucune
// connexion internet requise. Si ASPNETCORE_URLS est déjà défini (mécanisme standard ASP.NET Core),
// on le laisse prévaloir plutôt que d'imposer notre propre valeur par-dessus.
if (Environment.GetEnvironmentVariable("ASPNETCORE_URLS") == null)
    builder.WebHost.UseUrls(FkcScoring.Core.Data.NetworkConfig.ResolveUrls());

builder.Services.AddControllers().AddJsonOptions(o =>
{
    o.JsonSerializerOptions.PropertyNamingPolicy = System.Text.Json.JsonNamingPolicy.CamelCase;
    o.JsonSerializerOptions.DefaultIgnoreCondition = System.Text.Json.Serialization.JsonIgnoreCondition.WhenWritingNull;
    o.JsonSerializerOptions.Converters.Add(new System.Text.Json.Serialization.JsonStringEnumConverter());
});
builder.Services.AddEndpointsApiExplorer();
builder.Services.AddHttpContextAccessor();

var dbPath = FkcScoringPaths.ResolveDbPath();
Directory.CreateDirectory(Path.GetDirectoryName(dbPath)!);
builder.Services.AddDbContext<FkcScoringContext>(o => o.UseSqlite($"Data Source={dbPath}"));
builder.Services.AddScoped<AuditService>();
builder.Services.AddHostedService<KarateScoring.Api.SauvegardeAutomatiqueHostedService>();

var app = builder.Build();

// Deux postes peuvent toucher le même combat en parallèle (arbitre + poste de contrôle) : le jeton
// de concurrence (Combat/KataConfrontation.RowVersion) fait échouer le second SaveChanges plutôt que
// d'écraser silencieusement le premier — on transforme ça en réponse HTTP claire pour le frontend.
app.Use(async (context, next) =>
{
    try
    {
        await next();
    }
    catch (DbUpdateConcurrencyException)
    {
        context.Response.StatusCode = StatusCodes.Status409Conflict;
        await context.Response.WriteAsJsonAsync(new { detail = "Ces données ont été modifiées par un autre poste entre-temps. Rechargez et réessayez." });
    }
});

using (var scope = app.Services.CreateScope())
{
    var db = scope.ServiceProvider.GetRequiredService<FkcScoringContext>();
    db.Database.Migrate();
}

// Blocage total tant que ce poste n'est pas activé (cahier §21, "vendre sans se faire voler le
// logiciel") — seul /api/licence reste joignable, sans quoi personne ne pourrait jamais activer un
// poste neuf. Le frontend statique (index.html/app.js) reste servi tel quel : c'est lui qui affiche
// l'écran d'activation en lisant ce même statut via /api/licence.
app.Use(async (context, next) =>
{
    var path = context.Request.Path;
    if (!path.StartsWithSegments("/api") || path.StartsWithSegments("/api/licence")) { await next(); return; }

    using var scope = context.RequestServices.CreateScope();
    var db = scope.ServiceProvider.GetRequiredService<FkcScoringContext>();
    var parametres = await db.Parametres.FirstOrDefaultAsync(p => p.Id == 1);
    if (!LicenceService.ValiderCle(parametres?.LicenceCle, LicenceService.EmpreinteMachine()))
    {
        context.Response.StatusCode = StatusCodes.Status402PaymentRequired;
        await context.Response.WriteAsJsonAsync(new { detail = "Licence Karate Scoring non activée sur ce poste." });
        return;
    }
    await next();
});

app.UseDefaultFiles();
app.UseStaticFiles();
app.MapControllers();
app.MapFallbackToFile("index.html");

app.Run();

/// <summary>Rend la classe générée par les "top-level statements" (normalement <c>internal</c>) publique,
/// pour que <c>WebApplicationFactory&lt;Program&gt;</c> (tests d'intégration HTTP) puisse en hériter.</summary>
public partial class Program;
