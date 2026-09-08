using FkcScoring.Core.Data;
using FkcScoring.Core.Data.Entities;
using FkcScoring.Core.Domain;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace KarateScoring.Api.Controllers;

[ApiController]
[Route("api/clubs")]
public class ClubsController(FkcScoringContext db) : ControllerBase
{
    private static string UploadsDir => FkcScoringPaths.ResolveUploadsDir(FkcScoringPaths.ResolveDbPath());

    [HttpGet]
    public async Task<List<ClubDto>> GetAll() =>
        (await db.Clubs.OrderBy(c => c.Nom).ToListAsync()).Select(c => c.ToDto()).ToList();

    /// <summary>Récupère le club par nom (insensible à la casse) ou le crée — saisie libre côté frontend.</summary>
    [HttpPost("resolve")]
    public async Task<ActionResult<ClubDto>> Resolve(ResolveClubRequest req)
    {
        var nom = req.Nom.Trim();
        if (string.IsNullOrWhiteSpace(nom)) return BadRequest("Le nom du club est requis.");

        // Comparaison faite côté .NET (pas traduite en SQL) : ToLower() d'EF Core sur SQLite ne
        // gère correctement que l'ASCII, "Étoile" et "ÉTOILE" ne seraient pas reconnus identiques.
        var club = (await db.Clubs.ToListAsync()).FirstOrDefault(c => string.Equals(c.Nom, nom, StringComparison.OrdinalIgnoreCase));
        if (club == null)
        {
            club = new Club { Nom = nom };
            db.Clubs.Add(club);
            await db.SaveChangesAsync();
        }
        return club.ToDto();
    }

    [HttpPost("{id}/logo")]
    [RequestSizeLimit(ImageUploadService.TailleMaxOctets + 1024)]
    public async Task<IActionResult> UploaderLogo(int id, IFormFile fichier)
    {
        var club = await db.Clubs.FindAsync(id);
        if (club == null) return NotFound();
        if (fichier == null || fichier.Length == 0) return BadRequest("Aucun fichier reçu.");

        using var ms = new MemoryStream();
        await fichier.CopyToAsync(ms);
        var (ok, extension, erreur) = ImageUploadService.Valider(ms.ToArray());
        if (!ok) return BadRequest(erreur);

        ImageUploadService.Enregistrer(UploadsDir, "club", id, extension!, ms.ToArray(), club.LogoExtension);
        club.LogoExtension = extension;
        await db.SaveChangesAsync();
        return NoContent();
    }

    [HttpGet("{id}/logo")]
    public async Task<IActionResult> ObtenirLogo(int id)
    {
        var club = await db.Clubs.FindAsync(id);
        if (club?.LogoExtension == null) return NotFound();
        var chemin = ImageUploadService.CheminFichier(UploadsDir, "club", id, club.LogoExtension);
        if (!System.IO.File.Exists(chemin)) return NotFound();
        Response.Headers.CacheControl = "no-store";
        return PhysicalFile(chemin, ImageUploadService.ContentType(club.LogoExtension));
    }

    [HttpDelete("{id}/logo")]
    public async Task<IActionResult> SupprimerLogo(int id)
    {
        var club = await db.Clubs.FindAsync(id);
        if (club == null) return NotFound();
        if (club.LogoExtension != null)
        {
            ImageUploadService.Supprimer(UploadsDir, "club", id, club.LogoExtension);
            club.LogoExtension = null;
            await db.SaveChangesAsync();
        }
        return NoContent();
    }
}
