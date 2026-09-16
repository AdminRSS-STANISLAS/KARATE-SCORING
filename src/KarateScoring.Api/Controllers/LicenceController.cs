using FkcScoring.Core.Data;
using FkcScoring.Core.Data.Entities;
using FkcScoring.Core.Domain;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace KarateScoring.Api.Controllers;

/// <summary>Toujours accessible même sans licence active (voir le middleware de licence dans
/// Program.cs) — sans quoi personne ne pourrait jamais activer un poste tout neuf.</summary>
[ApiController]
[Route("api/licence")]
public class LicenceController(FkcScoringContext db) : ControllerBase
{
    [HttpGet]
    public async Task<LicenceStatusDto> Get()
    {
        var empreinte = LicenceService.EmpreinteMachine();
        var parametres = await db.Parametres.FirstOrDefaultAsync(p => p.Id == 1);
        var active = LicenceService.ValiderCle(parametres?.LicenceCle, empreinte);
        return new LicenceStatusDto(active, empreinte);
    }

    [HttpPost("activer")]
    public async Task<IActionResult> Activer(ActiverLicenceRequest req)
    {
        var empreinte = LicenceService.EmpreinteMachine();
        if (!LicenceService.ValiderCle(req.Cle, empreinte))
            return BadRequest("Clé d'activation invalide pour ce poste.");

        var parametres = await db.Parametres.FirstOrDefaultAsync(p => p.Id == 1) ?? new Parametres { Id = 1 };
        parametres.LicenceCle = req.Cle.Trim();
        if (db.Entry(parametres).State == EntityState.Detached) db.Add(parametres);
        await db.SaveChangesAsync();
        return NoContent();
    }
}
