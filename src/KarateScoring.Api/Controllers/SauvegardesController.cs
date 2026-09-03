using FkcScoring.Core.Data;
using FkcScoring.Core.Domain;
using FkcScoring.Core.Export;
using Microsoft.AspNetCore.Mvc;

namespace KarateScoring.Api.Controllers;

[ApiController]
[Route("api/sauvegardes")]
public class SauvegardesController(FkcScoringContext db, AuditService audit) : ControllerBase
{
    private static string DbPath => FkcScoringPaths.ResolveDbPath();
    private static string BackupDir => FkcScoringPaths.ResolveBackupDir(DbPath);

    [HttpGet]
    public ActionResult<List<SauvegardeInfo>> Lister() => SauvegardeService.ListerSauvegardes(BackupDir);

    /// <summary>Sauvegarde téléchargeable à la demande (pour clé USB/disque externe, cahier §25) — cohérente même pendant un usage actif (VACUUM INTO).</summary>
    [HttpGet("telecharger")]
    public IActionResult Telecharger()
    {
        var tempDir = Path.Combine(Path.GetTempPath(), "karate-scoring-export");
        var chemin = SauvegardeService.SauvegarderVersFichier(db, tempDir, "export");
        var nomTelechargement = $"karate-scoring_{DateTime.Now:yyyyMMdd_HHmmss}.db";
        var octets = System.IO.File.ReadAllBytes(chemin);
        System.IO.File.Delete(chemin);
        return File(octets, "application/octet-stream", nomTelechargement);
    }

    [HttpPost("{nom}/restaurer")]
    public async Task<IActionResult> Restaurer(string nom, RestaurerRequest req)
    {
        if (!await SecuriteService.VerifierCodeAsync(db, req.Code))
            return StatusCode(StatusCodes.Status403Forbidden, new { detail = "Code administrateur incorrect." });

        var cheminSource = Path.Combine(BackupDir, Path.GetFileName(nom));
        if (!System.IO.File.Exists(cheminSource)) return NotFound("Sauvegarde introuvable.");

        SauvegardeService.SauvegarderVersFichier(db, BackupDir, "avant_restauration");
        SauvegardeService.Restaurer(DbPath, cheminSource);
        audit.Consigner("Sauvegarde", 0, "Restauration", null, nom);
        return NoContent();
    }

    [HttpPost("importer")]
    [RequestSizeLimit(500_000_000)]
    public async Task<IActionResult> Importer(IFormFile fichier, [FromForm] string? code)
    {
        if (!await SecuriteService.VerifierCodeAsync(db, code))
            return StatusCode(StatusCodes.Status403Forbidden, new { detail = "Code administrateur incorrect." });
        if (fichier.Length == 0) return BadRequest("Fichier vide.");

        var cheminTemp = Path.Combine(Path.GetTempPath(), $"karate-scoring-import-{Guid.NewGuid()}.db");
        try
        {
            await using (var stream = System.IO.File.Create(cheminTemp))
                await fichier.CopyToAsync(stream);

            if (!SauvegardeService.EstFichierSqliteValide(cheminTemp))
                return BadRequest("Ce fichier ne semble pas être une sauvegarde Karate Scoring valide.");

            SauvegardeService.SauvegarderVersFichier(db, BackupDir, "avant_restauration");
            SauvegardeService.Restaurer(DbPath, cheminTemp);
            audit.Consigner("Sauvegarde", 0, "Import", null, fichier.FileName);
            return NoContent();
        }
        finally
        {
            if (System.IO.File.Exists(cheminTemp)) System.IO.File.Delete(cheminTemp);
        }
    }
}
