using FkcScoring.Core.Data;
using FkcScoring.Core.Data.Entities;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace KarateScoring.Api.Controllers;

[ApiController]
[Route("api")]
public class EquipesController(FkcScoringContext db) : ControllerBase
{
    [HttpGet("competitions/{competitionId}/equipes")]
    public async Task<List<EquipeDto>> GetAll(int competitionId)
    {
        var equipes = await db.Equipes
            .Include(e => e.Club)
            .Include(e => e.Membres).ThenInclude(m => m.Participant)
            .Where(e => e.Categorie!.CompetitionId == competitionId)
            .OrderBy(e => e.Nom)
            .ToListAsync();

        var result = new List<EquipeDto>();
        foreach (var e in equipes)
        {
            var categorieId = await db.Inscriptions.Where(i => i.EquipeId == e.Id).Select(i => (int?)i.CategorieId).FirstOrDefaultAsync();
            result.Add(e.ToDto(categorieId));
        }
        return result;
    }

    [HttpPost("competitions/{competitionId}/equipes")]
    public async Task<ActionResult<EquipeDto>> Create(int competitionId, CreateEquipeRequest req)
    {
        if (string.IsNullOrWhiteSpace(req.Nom)) return BadRequest("Le nom de l'équipe est requis.");
        if (string.IsNullOrWhiteSpace(req.Club)) return BadRequest("Le club est requis.");
        if (req.CategorieId == null || !await db.Categories.AnyAsync(c => c.Id == req.CategorieId && c.CompetitionId == competitionId))
            return BadRequest("Catégorie Kata équipe invalide pour cette compétition.");

        var clubNom = req.Club.Trim();
        var club = await db.Clubs.FirstOrDefaultAsync(c => c.Nom.ToLower() == clubNom.ToLower());
        if (club == null) { club = new Club { Nom = clubNom }; db.Clubs.Add(club); await db.SaveChangesAsync(); }

        var equipe = new Equipe { CategorieId = req.CategorieId.Value, Nom = req.Nom.Trim(), ClubId = club.Id };
        db.Equipes.Add(equipe);
        await db.SaveChangesAsync();

        foreach (var participantId in req.MembreIds.Distinct())
        {
            if (!await db.Participants.AnyAsync(p => p.Id == participantId)) continue;
            db.EquipeMembres.Add(new EquipeMembre { EquipeId = equipe.Id, ParticipantId = participantId });
        }
        db.Inscriptions.Add(new Inscription { CategorieId = req.CategorieId.Value, EquipeId = equipe.Id });
        await db.SaveChangesAsync();

        var membres = await db.EquipeMembres.Include(m => m.Participant).Where(m => m.EquipeId == equipe.Id).ToListAsync();
        equipe.Membres = membres;
        equipe.Club = club;
        return equipe.ToDto(req.CategorieId);
    }

    [HttpDelete("equipes/{id}")]
    public async Task<IActionResult> Delete(int id)
    {
        var equipe = await db.Equipes.FindAsync(id);
        if (equipe == null) return NotFound();
        db.Inscriptions.RemoveRange(db.Inscriptions.Where(i => i.EquipeId == id));
        db.EquipeMembres.RemoveRange(db.EquipeMembres.Where(m => m.EquipeId == id));
        db.Equipes.Remove(equipe);
        await db.SaveChangesAsync();
        return NoContent();
    }
}
