using FkcScoring.Core.Data;
using FkcScoring.Core.Data.Entities;
using FkcScoring.Core.Domain;
using FkcScoring.Core.Export;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace KarateScoring.Api.Controllers;

[ApiController]
[Route("api")]
public class ResultatsController(FkcScoringContext db) : ControllerBase
{
    [HttpGet("categories/{categorieId}/classement")]
    public async Task<ActionResult<List<ClassementDto>>> Classement(int categorieId)
    {
        var tableau = await db.Tableaux.FirstOrDefaultAsync(t => t.CategorieId == categorieId);
        if (tableau == null) return Ok(new List<ClassementDto>());

        var classement = new ClassementCalculator(db).CalculerEtEnregistrer(tableau.Id);
        var ids = classement.Select(c => c.Id).ToList();
        var avecNavs = await db.Classements
            .Include(c => c.Participant).ThenInclude(p => p!.Club)
            .Include(c => c.Equipe).ThenInclude(e => e!.Club)
            .Where(c => ids.Contains(c.Id))
            .OrderBy(c => c.Position)
            .ToListAsync();
        return avecNavs.Select(c => c.ToDto()).ToList();
    }

    [HttpGet("competitions/{competitionId}/export/kumite")]
    public async Task<IActionResult> ExportKumite(int competitionId, [FromQuery] string? categorie)
    {
        var competition = await db.Competitions.FindAsync(competitionId);
        if (competition == null) return NotFound();

        var categorieIds = await db.Categories.Where(c => c.CompetitionId == competitionId && c.Discipline == Discipline.KumiteIndividuel)
            .Where(c => categorie == null || c.Nom == categorie).Select(c => c.Id).ToListAsync();
        var tableauIds = await db.Tableaux.Where(t => categorieIds.Contains(t.CategorieId)).Select(t => t.Id).ToListAsync();
        var combats = await db.Combats
            .Include(c => c.CompetiteurAka).ThenInclude(p => p!.Club)
            .Include(c => c.CompetiteurAo).ThenInclude(p => p!.Club)
            .Include(c => c.Evenements)
            .Where(c => tableauIds.Contains(c.TableauId) && c.Statut == StatutCombat.Termine && !c.EstBye)
            .ToListAsync();

        var pdf = new KumiteReportBuilder().Generer(competition, combats, categorie);
        return File(pdf, "application/pdf", $"rapport-kumite-{competition.Nom}.pdf");
    }

    [HttpGet("competitions/{competitionId}/export/kata")]
    public async Task<IActionResult> ExportKata(int competitionId, [FromQuery] string? categorie)
    {
        var competition = await db.Competitions.FindAsync(competitionId);
        if (competition == null) return NotFound();

        var categories = await db.Categories.Where(c => c.CompetitionId == competitionId && c.Discipline != Discipline.KumiteIndividuel)
            .Where(c => categorie == null || c.Nom == categorie).ToListAsync();
        var categorieIds = categories.Select(c => c.Id).ToList();
        var tableauIds = await db.Tableaux.Where(t => categorieIds.Contains(t.CategorieId)).Select(t => t.Id).ToListAsync();
        var confrontations = await db.KataConfrontations
            .Include(c => c.Participant1).ThenInclude(p => p!.Club)
            .Include(c => c.Participant2).ThenInclude(p => p!.Club)
            .Include(c => c.Equipe1).ThenInclude(e => e!.Club)
            .Include(c => c.Equipe2).ThenInclude(e => e!.Club)
            .Include(c => c.Kata1)
            .Include(c => c.Kata2)
            .Include(c => c.Votes)
            .Where(c => tableauIds.Contains(c.TableauId) && c.Statut == StatutCombat.Termine && !c.EstBye)
            .ToListAsync();
        var disciplineParTableau = await db.Tableaux.Where(t => tableauIds.Contains(t.Id))
            .Join(categories, t => t.CategorieId, c => c.Id, (t, c) => new { t.Id, c.Discipline })
            .ToDictionaryAsync(x => x.Id, x => x.Discipline);

        var pdf = new KataReportBuilder().Generer(competition, confrontations, disciplineParTableau, categorie);
        return File(pdf, "application/pdf", $"rapport-kata-{competition.Nom}.pdf");
    }

    [HttpGet("competitions/{competitionId}/export/excel")]
    public async Task<IActionResult> ExportExcel(int competitionId)
    {
        var competition = await db.Competitions.FindAsync(competitionId);
        if (competition == null) return NotFound();

        var categories = await db.Categories.Where(c => c.CompetitionId == competitionId).ToListAsync();
        var calculator = new ClassementCalculator(db);
        var resultats = new List<(Categorie, List<Classement>)>();
        foreach (var categorie in categories)
        {
            var tableau = await db.Tableaux.FirstOrDefaultAsync(t => t.CategorieId == categorie.Id);
            if (tableau == null) continue;
            var classement = calculator.CalculerEtEnregistrer(tableau.Id);
            var ids = classement.Select(c => c.Id).ToList();
            var avecNavs = await db.Classements
                .Include(c => c.Participant).ThenInclude(p => p!.Club)
                .Include(c => c.Equipe).ThenInclude(e => e!.Club)
                .Where(c => ids.Contains(c.Id)).ToListAsync();
            resultats.Add((categorie, avecNavs));
        }

        var xlsx = new ResultatsExcelExporter().Generer(competition, resultats);
        return File(xlsx, "application/vnd.openxmlformats-officedocument.spreadsheetml.sheet", $"resultats-{competition.Nom}.xlsx");
    }
}
