using FkcScoring.Core.Data;
using FkcScoring.Core.Data.Entities;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace KarateScoring.Api.Controllers;

[ApiController]
[Route("api/clubs")]
public class ClubsController(FkcScoringContext db) : ControllerBase
{
    [HttpGet]
    public async Task<List<ClubDto>> GetAll() =>
        (await db.Clubs.OrderBy(c => c.Nom).ToListAsync()).Select(c => c.ToDto()).ToList();

    /// <summary>Récupère le club par nom (insensible à la casse) ou le crée — saisie libre côté frontend.</summary>
    [HttpPost("resolve")]
    public async Task<ActionResult<ClubDto>> Resolve(ResolveClubRequest req)
    {
        var nom = req.Nom.Trim();
        if (string.IsNullOrWhiteSpace(nom)) return BadRequest("Le nom du club est requis.");

        var club = await db.Clubs.FirstOrDefaultAsync(c => c.Nom.ToLower() == nom.ToLower());
        if (club == null)
        {
            club = new Club { Nom = nom };
            db.Clubs.Add(club);
            await db.SaveChangesAsync();
        }
        return club.ToDto();
    }
}
