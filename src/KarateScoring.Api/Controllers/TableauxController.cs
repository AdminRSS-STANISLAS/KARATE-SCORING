using FkcScoring.Core.Data;
using FkcScoring.Core.Data.Entities;
using FkcScoring.Core.Domain;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace KarateScoring.Api.Controllers;

[ApiController]
[Route("api")]
public class TableauxController(FkcScoringContext db, AuditService audit) : ControllerBase
{
    [HttpGet("categories/{categorieId}/tableau")]
    public async Task<ActionResult<TableauDto?>> Get(int categorieId)
    {
        var categorie = await db.Categories.FindAsync(categorieId);
        if (categorie == null) return NotFound();
        var tableau = await db.Tableaux.FirstOrDefaultAsync(t => t.CategorieId == categorieId);
        if (tableau == null) return Ok(null);
        return Ok(await BuildDto(tableau, categorie));
    }

    [HttpPost("categories/{categorieId}/tableau/generer")]
    public async Task<ActionResult<TableauDto>> Generer(int categorieId, GenererTableauRequest req)
    {
        var categorie = await db.Categories.FindAsync(categorieId);
        if (categorie == null) return NotFound("Catégorie introuvable.");
        if (await db.Tableaux.AnyAsync(t => t.CategorieId == categorieId))
            return BadRequest("Un tableau existe déjà pour cette catégorie.");
        var competition = await db.Competitions.FindAsync(categorie.CompetitionId);
        if (competition == null) return NotFound("Compétition introuvable.");

        var isEquipe = categorie.Discipline == Discipline.KataEquipe;
        var ids = isEquipe
            ? await db.Inscriptions.Where(i => i.CategorieId == categorieId && i.EquipeId != null).Select(i => i.EquipeId!.Value).ToListAsync()
            : await db.Inscriptions.Where(i => i.CategorieId == categorieId && i.ParticipantId != null).Select(i => i.ParticipantId!.Value).ToListAsync();
        if (ids.Count < 2) return BadRequest("Il faut au moins 2 inscrits pour générer un tableau.");

        var seuils = new TableauFormatSeuils { MaxPouleUnique = competition.SeuilPouleUnique, MaxPoulePuisElimination = competition.SeuilPoulePuisElimination };
        if (req.FormatForce != null && Enum.TryParse<FormatTableau>(req.FormatForce, out var force))
        {
            // Bascule manuelle : les seuils sont contournés en fixant les deux bornes à l'effectif exact voulu (cahier 5.3, dernier point).
            seuils = force switch
            {
                FormatTableau.PouleUnique => new TableauFormatSeuils { MaxPouleUnique = ids.Count, MaxPoulePuisElimination = ids.Count },
                FormatTableau.PoulePuisElimination => new TableauFormatSeuils { MaxPouleUnique = 0, MaxPoulePuisElimination = ids.Count },
                FormatTableau.EliminationRepechage => new TableauFormatSeuils { MaxPouleUnique = 0, MaxPoulePuisElimination = 0 },
                _ => seuils
            };
        }

        Tableau tableau;
        if (categorie.Discipline == Discipline.KumiteIndividuel)
        {
            tableau = new TableauService(db).GenererTableau(categorieId, ids, seuils);
        }
        else
        {
            var nbJuges = categorie.NbJugesKata ?? competition.NbJugesKataDefaut;
            tableau = new KataTableauService(db).GenererTableau(categorieId, ids, categorie.Discipline, nbJuges, seuils);
        }

        audit.Consigner("Tableau", tableau.Id, "Génération", null, $"{ids.Count} inscrits — format {tableau.Format}");
        return Ok(await BuildDto(tableau, categorie));
    }

    [HttpDelete("tableaux/{tableauId}")]
    public async Task<IActionResult> Delete(int tableauId)
    {
        var tableau = await db.Tableaux.FindAsync(tableauId);
        if (tableau == null) return NotFound();
        db.Combats.RemoveRange(db.Combats.Where(c => c.TableauId == tableauId));
        db.KataConfrontations.RemoveRange(db.KataConfrontations.Where(c => c.TableauId == tableauId));
        db.Classements.RemoveRange(db.Classements.Where(c => c.CategorieId == tableau.CategorieId));
        db.Tableaux.Remove(tableau);
        await db.SaveChangesAsync();
        audit.Consigner("Tableau", tableauId, "Régénéré (tableau précédent supprimé)");
        return NoContent();
    }

    [HttpPost("tableaux/{tableauId}/phase-elimination")]
    public async Task<ActionResult<TableauDto>> PhaseElimination(int tableauId)
    {
        var tableau = await db.Tableaux.FindAsync(tableauId);
        if (tableau == null) return NotFound();
        var categorie = await db.Categories.FindAsync(tableau.CategorieId);
        if (categorie == null) return NotFound();
        var competition = await db.Competitions.FindAsync(categorie.CompetitionId);
        if (competition == null) return NotFound();

        try
        {
            if (categorie.Discipline == Discipline.KumiteIndividuel)
                new TableauService(db).GenererPhaseEliminationApresPoules(tableauId);
            else
                new KataTableauService(db).GenererPhaseEliminationApresPoules(tableauId, categorie.Discipline, categorie.NbJugesKata ?? competition.NbJugesKataDefaut);
        }
        catch (InvalidOperationException ex) { return BadRequest(ex.Message); }

        audit.Consigner("Tableau", tableauId, "Phase à élimination directe générée");
        return Ok(await BuildDto(tableau, categorie));
    }

    private async Task<TableauDto> BuildDto(Tableau tableau, Categorie categorie)
    {
        List<ConfrontationDto> confrontations;
        if (categorie.Discipline == Discipline.KumiteIndividuel)
        {
            var combats = await db.Combats
                .Include(c => c.CompetiteurAka).ThenInclude(p => p!.Club)
                .Include(c => c.CompetiteurAo).ThenInclude(p => p!.Club)
                .Include(c => c.Evenements)
                .Where(c => c.TableauId == tableau.Id)
                .ToListAsync();
            confrontations = combats.OrderBy(c => c.EstRepechage).ThenBy(c => c.Tour).Select(c => c.ToDto()).ToList();
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
                .Where(c => c.TableauId == tableau.Id)
                .ToListAsync();
            confrontations = confs.OrderBy(c => c.EstRepechage).ThenBy(c => c.Tour).Select(c => c.ToDto(categorie.Discipline)).ToList();
        }
        return new TableauDto(tableau.Id, tableau.CategorieId, tableau.Format.ToString(), confrontations);
    }
}
