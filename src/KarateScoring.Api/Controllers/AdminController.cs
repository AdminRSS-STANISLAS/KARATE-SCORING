using FkcScoring.Core.Data;
using FkcScoring.Core.Export;
using Microsoft.AspNetCore.Mvc;

namespace KarateScoring.Api.Controllers;

[ApiController]
[Route("api/admin")]
public class AdminController(FkcScoringContext db) : ControllerBase
{
    /// <summary>
    /// Vide toutes les données de la plateforme (toutes compétitions confondues), après avoir sauvegardé
    /// une copie horodatée du fichier SQLite. Si la sauvegarde échoue, la base n'est pas touchée.
    /// </summary>
    [HttpPost("reset")]
    public IActionResult Reset()
    {
        var dbPath = FkcScoringPaths.ResolveDbPath();
        var backupDir = FkcScoringPaths.ResolveBackupDir(dbPath);
        var resetService = new DatabaseResetService(dbPath, backupDir);

        string backupPath;
        try
        {
            backupPath = resetService.SauvegarderBaseVersFichier();
        }
        catch (Exception ex)
        {
            return Problem($"Sauvegarde impossible, la base n'a pas été modifiée : {ex.Message}", statusCode: StatusCodes.Status500InternalServerError);
        }

        resetService.ViderCompetition(db);
        return Ok(new { backupPath });
    }
}
