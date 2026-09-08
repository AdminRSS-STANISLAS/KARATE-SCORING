using FkcScoring.Core.Data;
using FkcScoring.Core.Data.Entities;
using FkcScoring.Core.Domain;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace KarateScoring.Api.Controllers;

[ApiController]
[Route("api/participants")]
public class ParticipantsController(FkcScoringContext db) : ControllerBase
{
    private static string UploadsDir => FkcScoringPaths.ResolveUploadsDir(FkcScoringPaths.ResolveDbPath());

    [HttpGet]
    public async Task<List<ParticipantDto>> GetAll() =>
        (await db.Participants.Include(p => p.Club).OrderBy(p => p.Nom).ToListAsync()).Select(p => p.ToDto()).ToList();

    [HttpPost]
    public async Task<ActionResult<ParticipantDto>> Create(CreateParticipantRequest req)
    {
        if (string.IsNullOrWhiteSpace(req.Nom) || string.IsNullOrWhiteSpace(req.Prenom)) return BadRequest("Nom et prénom requis.");
        if (string.IsNullOrWhiteSpace(req.Club)) return BadRequest("Le club est requis.");

        var clubNom = req.Club.Trim();
        // Comparaison faite côté .NET (pas traduite en SQL) : ToLower() d'EF Core sur SQLite ne
        // gère correctement que l'ASCII, "Étoile" et "ÉTOILE" ne seraient pas reconnus identiques.
        var club = (await db.Clubs.ToListAsync()).FirstOrDefault(c => string.Equals(c.Nom, clubNom, StringComparison.OrdinalIgnoreCase));
        if (club == null) { club = new Club { Nom = clubNom }; db.Clubs.Add(club); await db.SaveChangesAsync(); }

        var participant = new Participant
        {
            Nom = req.Nom.Trim(), Prenom = req.Prenom.Trim(), ClubId = club.Id, Grade = req.Grade,
            DateNaissance = req.DateNaissance, NumeroLicence = req.Licence, PoidsKg = req.Poids
        };
        db.Participants.Add(participant);
        await db.SaveChangesAsync();
        participant.Club = club;
        return participant.ToDto();
    }

    [HttpDelete("{id}")]
    public async Task<IActionResult> Delete(int id)
    {
        var participant = await db.Participants.FindAsync(id);
        if (participant == null) return NotFound();
        if (participant.PhotoExtension != null) ImageUploadService.Supprimer(UploadsDir, "participant", id, participant.PhotoExtension);
        db.Inscriptions.RemoveRange(db.Inscriptions.Where(i => i.ParticipantId == id));
        db.EquipeMembres.RemoveRange(db.EquipeMembres.Where(m => m.ParticipantId == id));
        db.Participants.Remove(participant);
        await db.SaveChangesAsync();
        return NoContent();
    }

    [HttpPost("{id}/photo")]
    [RequestSizeLimit(ImageUploadService.TailleMaxOctets + 1024)]
    public async Task<IActionResult> UploaderPhoto(int id, IFormFile fichier)
    {
        var participant = await db.Participants.FindAsync(id);
        if (participant == null) return NotFound();
        if (fichier == null || fichier.Length == 0) return BadRequest("Aucun fichier reçu.");

        using var ms = new MemoryStream();
        await fichier.CopyToAsync(ms);
        var (ok, extension, erreur) = ImageUploadService.Valider(ms.ToArray());
        if (!ok) return BadRequest(erreur);

        ImageUploadService.Enregistrer(UploadsDir, "participant", id, extension!, ms.ToArray(), participant.PhotoExtension);
        participant.PhotoExtension = extension;
        await db.SaveChangesAsync();
        return NoContent();
    }

    [HttpGet("{id}/photo")]
    public async Task<IActionResult> ObtenirPhoto(int id)
    {
        var participant = await db.Participants.FindAsync(id);
        if (participant?.PhotoExtension == null) return NotFound();
        var chemin = ImageUploadService.CheminFichier(UploadsDir, "participant", id, participant.PhotoExtension);
        if (!System.IO.File.Exists(chemin)) return NotFound();
        Response.Headers.CacheControl = "no-store";
        return PhysicalFile(chemin, ImageUploadService.ContentType(participant.PhotoExtension));
    }

    [HttpDelete("{id}/photo")]
    public async Task<IActionResult> SupprimerPhoto(int id)
    {
        var participant = await db.Participants.FindAsync(id);
        if (participant == null) return NotFound();
        if (participant.PhotoExtension != null)
        {
            ImageUploadService.Supprimer(UploadsDir, "participant", id, participant.PhotoExtension);
            participant.PhotoExtension = null;
            await db.SaveChangesAsync();
        }
        return NoContent();
    }

    [HttpGet("{id}/inscriptions")]
    public async Task<ActionResult<List<int>>> GetInscriptions(int id)
    {
        if (!await db.Participants.AnyAsync(p => p.Id == id)) return NotFound();
        return await db.Inscriptions.Where(i => i.ParticipantId == id).Select(i => i.CategorieId).ToListAsync();
    }

    [HttpPut("{id}/inscriptions")]
    public async Task<IActionResult> SetInscriptions(int id, InscriptionsRequest req)
    {
        if (!await db.Participants.AnyAsync(p => p.Id == id)) return NotFound();

        var actuelles = await db.Inscriptions.Where(i => i.ParticipantId == id).ToListAsync();
        db.Inscriptions.RemoveRange(actuelles.Where(i => !req.CategorieIds.Contains(i.CategorieId)));
        var existantes = actuelles.Select(i => i.CategorieId).ToHashSet();
        foreach (var categorieId in req.CategorieIds.Where(c => !existantes.Contains(c)))
        {
            if (!await db.Categories.AnyAsync(c => c.Id == categorieId)) continue;
            db.Inscriptions.Add(new Inscription { ParticipantId = id, CategorieId = categorieId });
        }
        await db.SaveChangesAsync();
        return NoContent();
    }
}
