using FkcScoring.Core.Data;
using FkcScoring.Core.Data.Entities;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace KarateScoring.Api.Controllers;

[ApiController]
[Route("api/competitions")]
public class CompetitionsController(FkcScoringContext db) : ControllerBase
{
    [HttpGet]
    public async Task<List<CompetitionDto>> GetAll() =>
        (await db.Competitions.OrderByDescending(c => c.Date).ToListAsync()).Select(c => c.ToDto()).ToList();

    [HttpGet("{id}")]
    public async Task<ActionResult<CompetitionDto>> GetOne(int id)
    {
        var c = await db.Competitions.FindAsync(id);
        return c == null ? NotFound() : c.ToDto();
    }

    [HttpPost]
    public async Task<ActionResult<CompetitionDto>> Create(CreateCompetitionRequest req)
    {
        if (string.IsNullOrWhiteSpace(req.Nom)) return BadRequest("Le nom est requis.");
        if (!Enum.TryParse<NiveauCompetition>(req.Niveau, out var niveau)) return BadRequest("Niveau invalide.");

        var competition = new Competition { Nom = req.Nom.Trim(), Date = req.Date, Lieu = req.Lieu, Niveau = niveau };
        db.Competitions.Add(competition);
        await db.SaveChangesAsync();
        return competition.ToDto();
    }

    [HttpPut("{id}/reglages")]
    public async Task<ActionResult<CompetitionDto>> Reglages(int id, ReglagesRequest req)
    {
        var c = await db.Competitions.FindAsync(id);
        if (c == null) return NotFound();
        c.DureeCombatDefautSec = req.DureeCombatDefautSec;
        c.EcartVictoire = req.EcartVictoire;
        c.NbJugesKataDefaut = req.NbJugesKataDefaut;
        c.SeuilPouleUnique = req.SeuilPouleUnique;
        c.SeuilPoulePuisElimination = req.SeuilPoulePuisElimination;
        await db.SaveChangesAsync();
        return c.ToDto();
    }
}
