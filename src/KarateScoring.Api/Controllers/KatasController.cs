using FkcScoring.Core.Data;
using FkcScoring.Core.Data.Entities;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace KarateScoring.Api.Controllers;

[ApiController]
[Route("api/katas")]
public class KatasController(FkcScoringContext db) : ControllerBase
{
    [HttpGet]
    public async Task<List<KataDto>> GetAll() =>
        (await db.Katas.Where(k => k.Actif).OrderBy(k => k.Nom).ToListAsync()).Select(k => k.ToDto()).ToList();

    [HttpPost]
    public async Task<ActionResult<KataDto>> Create(CreateKataRequest req)
    {
        if (string.IsNullOrWhiteSpace(req.Nom)) return BadRequest("Le nom du kata est requis.");
        var nom = req.Nom.Trim();

        // Comparaison faite côté .NET (pas traduite en SQL), même raison que ClubsController.Resolve :
        // ToLower() d'EF Core sur SQLite ne gère correctement que l'ASCII. Réutilise un kata existant
        // (même désactivé) plutôt que d'en créer un doublon indiscernable dans la liste déroulante.
        var existant = (await db.Katas.ToListAsync()).FirstOrDefault(k => string.Equals(k.Nom, nom, StringComparison.OrdinalIgnoreCase));
        if (existant != null)
        {
            if (!existant.Actif) existant.Actif = true;
            await db.SaveChangesAsync();
            return existant.ToDto();
        }

        var kata = new Kata { Nom = nom, Actif = true };
        db.Katas.Add(kata);
        await db.SaveChangesAsync();
        return kata.ToDto();
    }

    [HttpDelete("{id}")]
    public async Task<IActionResult> Retirer(int id)
    {
        var kata = await db.Katas.FindAsync(id);
        if (kata == null) return NotFound();
        kata.Actif = false; // conserve les confrontations passées qui référencent ce kata
        await db.SaveChangesAsync();
        return NoContent();
    }
}
