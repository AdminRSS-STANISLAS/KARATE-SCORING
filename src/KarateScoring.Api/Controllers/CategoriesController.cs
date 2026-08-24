using FkcScoring.Core.Data;
using FkcScoring.Core.Data.Entities;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace KarateScoring.Api.Controllers;

[ApiController]
[Route("api")]
public class CategoriesController(FkcScoringContext db) : ControllerBase
{
    [HttpGet("competitions/{competitionId}/categories")]
    public async Task<List<CategorieDto>> GetAll(int competitionId)
    {
        var categories = await db.Categories.Where(c => c.CompetitionId == competitionId).OrderBy(c => c.Nom).ToListAsync();
        var result = new List<CategorieDto>();
        foreach (var c in categories)
        {
            var inscrits = await db.Inscriptions.CountAsync(i => i.CategorieId == c.Id);
            var hasTableau = await db.Tableaux.AnyAsync(t => t.CategorieId == c.Id);
            result.Add(new CategorieDto(c.Id, c.CompetitionId, c.Nom, c.Discipline.ToString(), c.Sexe, c.AgeMin, c.AgeMax, c.GradeMin, inscrits, hasTableau));
        }
        return result;
    }

    [HttpPost("competitions/{competitionId}/categories")]
    public async Task<ActionResult<CategorieDto>> Create(int competitionId, CreateCategorieRequest req)
    {
        if (string.IsNullOrWhiteSpace(req.Nom)) return BadRequest("Le nom est requis.");
        if (!Enum.TryParse<Discipline>(req.Discipline, out var discipline)) return BadRequest("Discipline invalide.");
        if (!await db.Competitions.AnyAsync(c => c.Id == competitionId)) return NotFound("Compétition introuvable.");

        var categorie = new Categorie
        {
            CompetitionId = competitionId, Nom = req.Nom.Trim(), Discipline = discipline, Sexe = req.Sexe,
            AgeMin = req.AgeMin, AgeMax = req.AgeMax, GradeMin = req.GradeMin
        };
        db.Categories.Add(categorie);
        await db.SaveChangesAsync();
        return new CategorieDto(categorie.Id, competitionId, categorie.Nom, categorie.Discipline.ToString(), categorie.Sexe, categorie.AgeMin, categorie.AgeMax, categorie.GradeMin, 0, false);
    }

    [HttpDelete("categories/{id}")]
    public async Task<IActionResult> Delete(int id)
    {
        var categorie = await db.Categories.FindAsync(id);
        if (categorie == null) return NotFound();

        var tableauIds = await db.Tableaux.Where(t => t.CategorieId == id).Select(t => t.Id).ToListAsync();
        db.Combats.RemoveRange(db.Combats.Where(c => tableauIds.Contains(c.TableauId)));
        db.KataConfrontations.RemoveRange(db.KataConfrontations.Where(c => tableauIds.Contains(c.TableauId)));
        db.Tableaux.RemoveRange(db.Tableaux.Where(t => t.CategorieId == id));
        db.Inscriptions.RemoveRange(db.Inscriptions.Where(i => i.CategorieId == id));
        db.Classements.RemoveRange(db.Classements.Where(c => c.CategorieId == id));
        db.Categories.Remove(categorie);
        await db.SaveChangesAsync();
        return NoContent();
    }
}
