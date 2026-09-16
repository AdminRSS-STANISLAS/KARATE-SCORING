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
    private static string UploadsDir => FkcScoringPaths.ResolveUploadsDir(DbPath);
    private static string TempDir => Path.Combine(Path.GetTempPath(), "karate-scoring-export");

    [HttpGet]
    public ActionResult<List<SauvegardeInfo>> Lister() => SauvegardeService.ListerSauvegardes(BackupDir);

    /// <summary>
    /// Sauvegarde téléchargeable à la demande (pour clé USB/disque externe, transfert vers un autre poste
    /// Karate Scoring — cahier §25) — base + photos/logos importés dans une seule archive, cohérente même
    /// pendant un usage actif (VACUUM INTO). Une archive complète, pas juste la base : les photos vivent en
    /// fichiers séparés sur disque, un export "base seule" les perdrait silencieusement.
    /// </summary>
    [HttpGet("telecharger")]
    public IActionResult Telecharger()
    {
        var octets = SauvegardeService.CreerArchiveTransfert(db, UploadsDir, TempDir);
        var nomTelechargement = $"karate-scoring_{DateTime.Now:yyyyMMdd_HHmmss}.zip";
        return File(octets, "application/zip", nomTelechargement);
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

    /// <summary>Accepte une archive .zip produite par <c>/telecharger</c> (base + photos/logos) ou, pour
    /// compatibilité, un simple fichier .db issu d'un export précédent (dans ce cas les photos ne sont pas
    /// touchées).</summary>
    [HttpPost("importer")]
    [RequestSizeLimit(500_000_000)]
    public async Task<IActionResult> Importer(IFormFile fichier, [FromForm] string? code)
    {
        if (!await SecuriteService.VerifierCodeAsync(db, code))
            return StatusCode(StatusCodes.Status403Forbidden, new { detail = "Code administrateur incorrect." });
        if (fichier.Length == 0) return BadRequest("Fichier vide.");

        byte[] octets;
        using (var ms = new MemoryStream())
        {
            await fichier.CopyToAsync(ms);
            octets = ms.ToArray();
        }

        var estZip = octets.Length >= 2 && octets[0] == 'P' && octets[1] == 'K';
        if (!estZip)
        {
            Directory.CreateDirectory(TempDir);
            var cheminValidation = Path.Combine(TempDir, $"validation_{Guid.NewGuid():N}.db");
            try
            {
                await System.IO.File.WriteAllBytesAsync(cheminValidation, octets);
                if (!SauvegardeService.EstFichierSqliteValide(cheminValidation))
                    return BadRequest("Ce fichier ne semble pas être une sauvegarde Karate Scoring valide.");
            }
            finally
            {
                if (System.IO.File.Exists(cheminValidation)) System.IO.File.Delete(cheminValidation);
            }
        }

        try
        {
            SauvegardeService.SauvegarderVersFichier(db, BackupDir, "avant_restauration");
            SauvegardeService.RestaurerArchiveTransfert(DbPath, UploadsDir, octets, TempDir);
            audit.Consigner("Sauvegarde", 0, "Import", null, fichier.FileName);
            return NoContent();
        }
        catch (Exception ex) when (ex is InvalidOperationException or System.IO.InvalidDataException)
        {
            return BadRequest("Ce fichier ne semble pas être une sauvegarde Karate Scoring valide : " + ex.Message);
        }
    }
}
