using Microsoft.AspNetCore.Mvc.Testing;

namespace KarateScoring.Api.Tests;

/// <summary>
/// Démarre l'application réelle (Program.cs, vrais contrôleurs, vraie base SQLite) contre un fichier
/// temporaire dédié à ce fixture — <see cref="FkcScoring.Core.Data.FkcScoringPaths.ResolveDbPath"/> lit
/// la variable d'environnement <c>KARATE_SCORING_DB_PATH</c> au démarrage, donc la définir avant que le
/// host ne soit construit suffit à isoler chaque run sans toucher au code de production.
/// </summary>
public class ApiFactory : WebApplicationFactory<Program>, IDisposable
{
    // Un sous-dossier dédié par instance (pas juste un fichier) : FkcScoringPaths.ResolveBackupDir place
    // les sauvegardes automatiques/manuelles dans "<dossier-de-la-base>/backups" — sans isolation par
    // dossier, plusieurs runs de tests partageraient et se marcheraient dessus sur ce sous-dossier.
    private readonly string _dir = Path.Combine(Path.GetTempPath(), $"karate-scoring-tests-{Guid.NewGuid():N}");
    public string DbPath => Path.Combine(_dir, "karate-scoring.db");

    public ApiFactory()
    {
        Directory.CreateDirectory(_dir);
        Environment.SetEnvironmentVariable("KARATE_SCORING_DB_PATH", DbPath);
    }

    protected override void Dispose(bool disposing)
    {
        base.Dispose(disposing);
        if (Directory.Exists(_dir)) try { Directory.Delete(_dir, recursive: true); } catch { /* meilleur effort — un handle SQLite peut rester ouvert un instant après l'arrêt du host */ }
    }
}
