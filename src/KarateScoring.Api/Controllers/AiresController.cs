using FkcScoring.Core.Data;
using FkcScoring.Core.Data.Entities;
using FkcScoring.Core.Domain;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace KarateScoring.Api.Controllers;

[ApiController]
[Route("api")]
public class AiresController(FkcScoringContext db, AuditService audit) : ControllerBase
{
    [HttpGet("competitions/{competitionId}/aires")]
    public async Task<ActionResult<List<AireDto>>> Get(int competitionId)
    {
        var aires = await db.Aires.Where(a => a.CompetitionId == competitionId).OrderBy(a => a.Ordre).ToListAsync();
        return aires.Select(a => a.ToDto()).ToList();
    }

    [HttpPost("competitions/{competitionId}/aires")]
    public async Task<ActionResult<AireDto>> Create(int competitionId, CreateAireRequest req)
    {
        if (await db.Competitions.FindAsync(competitionId) == null) return NotFound("Compétition introuvable.");
        var nom = (req.Nom ?? "").Trim();
        if (nom.Length == 0) return BadRequest("Le nom de l'aire est requis.");

        var ordre = 1 + await db.Aires.Where(a => a.CompetitionId == competitionId).Select(a => (int?)a.Ordre).MaxAsync() ?? 0;
        var aire = new Aire { CompetitionId = competitionId, Nom = nom, Ordre = ordre };
        db.Aires.Add(aire);
        await db.SaveChangesAsync();
        audit.Consigner("Aire", aire.Id, "Création", null, nom);
        return Ok(aire.ToDto());
    }

    [HttpPut("aires/{id}")]
    public async Task<ActionResult<AireDto>> Rename(int id, RenameAireRequest req)
    {
        var aire = await db.Aires.FindAsync(id);
        if (aire == null) return NotFound();
        var nom = (req.Nom ?? "").Trim();
        if (nom.Length == 0) return BadRequest("Le nom de l'aire est requis.");

        var ancien = aire.Nom;
        aire.Nom = nom;
        await db.SaveChangesAsync();
        audit.Consigner("Aire", id, "Renommée", ancien, nom);
        return Ok(aire.ToDto());
    }

    [HttpDelete("aires/{id}")]
    public async Task<IActionResult> Delete(int id)
    {
        var aire = await db.Aires.FindAsync(id);
        if (aire == null) return NotFound();
        db.Aires.Remove(aire);
        await db.SaveChangesAsync();
        audit.Consigner("Aire", id, "Suppression (tableaux assignés désassignés)", aire.Nom);
        return NoContent();
    }

    [HttpGet("aires/{id}/file-attente")]
    public async Task<ActionResult<FileAttenteDto>> FileAttente(int id)
    {
        var aire = await db.Aires.FindAsync(id);
        if (aire == null) return NotFound();

        var tableaux = await db.Tableaux.Where(t => t.AireId == id).Include(t => t.Categorie).ToListAsync();
        var entries = new List<FileAttenteEntryDto>();

        foreach (var tableau in tableaux)
        {
            var categorie = tableau.Categorie!;
            if (categorie.Discipline == Discipline.KumiteIndividuel)
            {
                var combats = await db.Combats
                    .Include(c => c.CompetiteurAka).ThenInclude(p => p!.Club)
                    .Include(c => c.CompetiteurAo).ThenInclude(p => p!.Club)
                    .Include(c => c.Evenements)
                    .Where(c => c.TableauId == tableau.Id && !c.EstBye && c.CompetiteurAkaId != null && c.CompetiteurAoId != null)
                    .OrderBy(c => c.EstRepechage).ThenBy(c => c.Tour).ThenBy(c => c.Id)
                    .ToListAsync();
                entries.AddRange(combats.Select(c => new FileAttenteEntryDto(categorie.Nom, c.ToDto())));
            }
            else
            {
                var confs = await db.KataConfrontations
                    .Include(c => c.Participant1).ThenInclude(p => p!.Club)
                    .Include(c => c.Participant2).ThenInclude(p => p!.Club)
                    .Include(c => c.Equipe1).ThenInclude(e => e!.Club)
                    .Include(c => c.Equipe2).ThenInclude(e => e!.Club)
                    .Include(c => c.Kata1)
                    .Include(c => c.Kata2)
                    .Include(c => c.Votes)
                    .Where(c => c.TableauId == tableau.Id && !c.EstBye &&
                        ((c.Participant1Id != null && c.Participant2Id != null) || (c.Equipe1Id != null && c.Equipe2Id != null)))
                    .OrderBy(c => c.EstRepechage).ThenBy(c => c.Tour).ThenBy(c => c.Id)
                    .ToListAsync();
                entries.AddRange(confs.Select(c => new FileAttenteEntryDto(categorie.Nom, c.ToDto(categorie.Discipline))));
            }
        }

        var enCours = entries.FirstOrDefault(e => e.Confrontation.Statut == StatutCombat.EnCours.ToString());
        var restants = entries.Where(e => e.Confrontation.Statut == StatutCombat.EnAttente.ToString()).ToList();
        var suivant = restants.FirstOrDefault();
        var aVenir = suivant == null ? restants : restants.Skip(1).ToList();

        return new FileAttenteDto(aire.Nom, enCours, suivant, aVenir);
    }
}
