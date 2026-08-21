using FkcScoring.Core.Data;
using FkcScoring.Core.Data.Entities;
using FkcScoring.Core.Domain;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace KarateScoring.Api.Controllers;

[ApiController]
[Route("api/kata-confrontations")]
public class KataConfrontationsController(FkcScoringContext db, AuditService audit) : ControllerBase
{
    private async Task<(KataConfrontation? confrontation, Categorie? categorie)> Charger(int id)
    {
        var confrontation = await db.KataConfrontations
            .Include(c => c.Participant1).ThenInclude(p => p!.Club)
            .Include(c => c.Participant2).ThenInclude(p => p!.Club)
            .Include(c => c.Equipe1).ThenInclude(e => e!.Club)
            .Include(c => c.Equipe2).ThenInclude(e => e!.Club)
            .Include(c => c.Kata1)
            .Include(c => c.Kata2)
            .Include(c => c.Votes)
            .Include(c => c.Tableau)
            .FirstOrDefaultAsync(c => c.Id == id);
        if (confrontation == null) return (null, null);
        var categorie = await db.Categories.FindAsync(confrontation.Tableau!.CategorieId);
        return (confrontation, categorie);
    }

    [HttpGet("{id}")]
    public async Task<ActionResult<ConfrontationDto>> Get(int id)
    {
        var (confrontation, categorie) = await Charger(id);
        return confrontation == null || categorie == null ? NotFound() : confrontation.ToDto(categorie.Discipline);
    }

    [HttpPost("{id}/kata")]
    public async Task<ActionResult<ConfrontationDto>> DefinirKata(int id, DefinirKataRequest req)
    {
        var (confrontation, categorie) = await Charger(id);
        if (confrontation == null || categorie == null) return NotFound();
        if (!Enum.TryParse<Couleur>(req.Couleur, true, out var couleur)) return BadRequest("Couleur invalide.");
        if (!await db.Katas.AnyAsync(k => k.Id == req.KataId)) return BadRequest("Kata invalide.");

        KataEngine.DefinirKata(confrontation, couleur, req.KataId);
        await db.SaveChangesAsync();
        var kata = await db.Katas.FindAsync(req.KataId);
        audit.Consigner("KataConfrontation", id, $"Kata {req.Couleur} défini", null, kata?.Nom);
        return confrontation.ToDto(categorie.Discipline);
    }

    [HttpPost("{id}/vote")]
    public async Task<ActionResult<ConfrontationDto>> Voter(int id, VoteRequest req)
    {
        var (confrontation, categorie) = await Charger(id);
        if (confrontation == null || categorie == null) return NotFound();
        if (!Enum.TryParse<Couleur>(req.Couleur, true, out var couleur)) return BadRequest("Couleur invalide.");

        try { KataEngine.EnregistrerVote(confrontation, req.JugeNumero, couleur); }
        catch (Exception ex) when (ex is InvalidOperationException or ArgumentOutOfRangeException) { return BadRequest(ex.Message); }

        await db.SaveChangesAsync();
        audit.Consigner("KataConfrontation", id, $"Vote juge {req.JugeNumero}", null, req.Couleur);
        return confrontation.ToDto(categorie.Discipline);
    }

    [HttpPost("{id}/valider")]
    public async Task<ActionResult<ConfrontationDto>> Valider(int id)
    {
        var (confrontation, categorie) = await Charger(id);
        if (confrontation == null || categorie == null) return NotFound();

        try { KataEngine.ValiderResultat(confrontation); }
        catch (InvalidOperationException ex) { return BadRequest(ex.Message); }

        await db.SaveChangesAsync();
        new KataTableauService(db).EnregistrerResultat(confrontation, categorie.Discipline);
        audit.Consigner("KataConfrontation", id, "Tranchée par vote", null, confrontation.VainqueurCouleur.ToString());
        return confrontation.ToDto(categorie.Discipline);
    }
}
