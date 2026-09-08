using FkcScoring.Core.Data;
using FkcScoring.Core.Domain;
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
    /// Protégé par le code administrateur (§21) une fois configuré, sinon comportement inchangé —
    /// n'importe quel poste sur le réseau local peut sinon vider toute la base d'un tournoi en cours.
    /// </summary>
    [HttpPost("reset")]
    public async Task<IActionResult> Reset(ResetRequest? req)
    {
        if (!await SecuriteService.VerifierCodeAsync(db, req?.Code))
            return StatusCode(StatusCodes.Status403Forbidden, new { detail = "Code administrateur incorrect." });

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
