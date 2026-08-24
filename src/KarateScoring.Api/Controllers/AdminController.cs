using FkcScoring.Core.Data;
using FkcScoring.Core.Export;
using Microsoft.AspNetCore.Mvc;

namespace KarateScoring.Api.Controllers;

[ApiController]
[Route("api/admin")]
public class AdminController(FkcScoringContext db) : ControllerBase
{
    /// <summary>Vide toutes les données (cahier : filet de sécurité "exporter puis réinitialiser") — à utiliser depuis l'écran d'administration, après export.</summary>
    [HttpPost("reset")]
    public IActionResult Reset()
    {
        var dbPath = Environment.GetEnvironmentVariable("KARATE_SCORING_DB_PATH") ?? "/data/karate-scoring.db";
        var backupDir = Path.Combine(Path.GetDirectoryName(dbPath) ?? "/data", "backups");
        new DatabaseResetService(dbPath, backupDir).ViderCompetition(db);
        return NoContent();
    }
}
