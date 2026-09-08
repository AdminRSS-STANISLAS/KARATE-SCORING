using FkcScoring.Core.Data;
using FkcScoring.Core.Data.Entities;
using FkcScoring.Core.Domain;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace KarateScoring.Api.Controllers;

[ApiController]
[Route("api/combats")]
public class CombatsController(FkcScoringContext db, AuditService audit) : ControllerBase
{
    private static readonly Dictionary<string, int> PointType = new() { ["ippon"] = 3, ["wazaari"] = 2, ["yuko"] = 1 };

    private async Task<Combat?> Charger(int id) => await db.Combats
        .Include(c => c.CompetiteurAka).ThenInclude(p => p!.Club)
        .Include(c => c.CompetiteurAo).ThenInclude(p => p!.Club)
        .Include(c => c.Evenements)
        .Include(c => c.Tableau)
        .FirstOrDefaultAsync(c => c.Id == id);

    private async Task<Competition> CompetitionDe(Combat combat)
    {
        var categorie = await db.Categories.FindAsync(combat.Tableau!.CategorieId);
        return (await db.Competitions.FindAsync(categorie!.CompetitionId))!;
    }

    private static bool EstEnPoule(Combat combat) =>
        combat.Tableau!.Format == FormatTableau.PouleUnique ||
        (combat.Tableau!.Format == FormatTableau.PoulePuisElimination && combat.Tour == 1);

    [HttpGet("{id}")]
    public async Task<ActionResult<ConfrontationDto>> Get(int id)
    {
        var combat = await Charger(id);
        return combat == null ? NotFound() : combat.ToDto();
    }

    /// <summary>
    /// Miroir pur d'affichage : reçoit l'état du chronomètre client (démarré/en pause, temps restant)
    /// pour qu'un écran public séparé puisse le reconstruire par polling. N'affecte ni le statut du
    /// combat ni le score — ne déclenche donc pas d'audit, contrairement aux vraies actions d'arbitrage.
    /// </summary>
    [HttpPost("{id}/chrono-sync")]
    public async Task<IActionResult> ChronoSync(int id, ChronoSyncRequest req)
    {
        var combat = await db.Combats.FindAsync(id);
        if (combat == null) return NotFound();
        combat.ChronoDemarreLeUtc = req.Running ? DateTime.UtcNow : null;
        combat.ChronoRestantMs = req.RemainingMs;
        await db.SaveChangesAsync();
        return NoContent();
    }

    [HttpPost("{id}/demarrer")]
    public async Task<ActionResult<ConfrontationDto>> Demarrer(int id)
    {
        var combat = await Charger(id);
        if (combat == null) return NotFound();
        CombatEngine.DemarrerCombat(combat);
        await db.SaveChangesAsync();
        return combat.ToDto();
    }

    [HttpPost("{id}/point")]
    public async Task<ActionResult<ConfrontationDto>> Point(int id, PointRequest req)
    {
        var combat = await Charger(id);
        if (combat == null) return NotFound();
        if (!Enum.TryParse<Couleur>(req.Couleur, true, out var couleur)) return BadRequest("Couleur invalide.");
        if (!PointType.TryGetValue(req.Type.ToLower(), out var barePoints)) return BadRequest("Type de point invalide.");

        var competition = await CompetitionDe(combat);
        var points = req.Type.ToLower() switch { "ippon" => competition.PointsIppon, "wazaari" => competition.PointsWazaAri, _ => competition.PointsYuko };

        // Le chronomètre vit côté client (pas de chrono serveur) ; le temps écoulé transmis par
        // l'arbitre au moment de l'action alimente l'historique horodaté (cahier 5.4).
        try { CombatEngine.AjouterPoint(combat, couleur, points, TimeSpan.FromSeconds(Math.Max(0, req.TempsEcouleSec))); }
        catch (InvalidOperationException ex) { return BadRequest(ex.Message); }

        var termine = CombatEngine.VerifierEcartVictoire(combat, competition.EcartVictoire);
        await db.SaveChangesAsync();
        audit.Consigner("Combat", id, $"Point {req.Type} {req.Couleur}", null, $"+{points}");
        if (termine)
        {
            new TableauService(db).EnregistrerResultatCombat(combat);
            audit.Consigner("Combat", id, "Terminé (écart de points)", null, combat.VainqueurCouleur.ToString());
        }
        return combat.ToDto();
    }

    [HttpPost("{id}/penalite")]
    public async Task<ActionResult<ConfrontationDto>> Penalite(int id, PenaliteRequest req)
    {
        var combat = await Charger(id);
        if (combat == null) return NotFound();
        if (!Enum.TryParse<Couleur>(req.Couleur, true, out var couleur)) return BadRequest("Couleur invalide.");
        if (!Enum.TryParse<TypePenalite>(req.Penalite, true, out var penalite)) return BadRequest("Pénalité invalide.");

        try { CombatEngine.AppliquerPenalite(combat, couleur, penalite, TimeSpan.FromSeconds(Math.Max(0, req.TempsEcouleSec))); }
        catch (InvalidOperationException ex) { return BadRequest(ex.Message); }
        await db.SaveChangesAsync();
        audit.Consigner("Combat", id, $"Pénalité {req.Penalite} {req.Couleur}");

        if (combat.Statut == StatutCombat.Termine)
        {
            new TableauService(db).EnregistrerResultatCombat(combat);
            audit.Consigner("Combat", id, "Terminé (disqualification)", null, combat.VainqueurCouleur.ToString());
        }
        return combat.ToDto();
    }

    [HttpPost("{id}/fin-de-temps")]
    public async Task<ActionResult<ConfrontationDto>> FinDeTemps(int id, FinDeTempsRequest? req)
    {
        var combat = await Charger(id);
        if (combat == null) return NotFound();
        // La durée réglementaire peut être ajustée par match depuis l'écran d'arbitrage (bouton
        // « Durée »), donc c'est le temps réellement écoulé côté chrono client qui fait foi ici,
        // pas la durée par défaut de la compétition.
        combat.DureeReelleSec = req != null && req.TempsEcouleSec > 0 ? req.TempsEcouleSec : (await CompetitionDe(combat)).DureeCombatDefautSec;

        CombatEngine.TerminerParFinDeTemps(combat, EstEnPoule(combat));
        await db.SaveChangesAsync();

        if (combat.Statut == StatutCombat.Termine)
        {
            new TableauService(db).EnregistrerResultatCombat(combat);
            audit.Consigner("Combat", id, "Terminé (fin de temps)", null, combat.VainqueurCouleur.ToString());
        }
        else
        {
            audit.Consigner("Combat", id, "Fin de temps — égalité, Hantei en attente");
        }
        return combat.ToDto();
    }

    [HttpPost("{id}/hantei")]
    public async Task<ActionResult<ConfrontationDto>> Hantei(int id, HanteiRequest req)
    {
        var combat = await Charger(id);
        if (combat == null) return NotFound();
        if (!Enum.TryParse<Couleur>(req.Couleur, true, out var couleur)) return BadRequest("Couleur invalide.");

        try { CombatEngine.EnregistrerHantei(combat, couleur); }
        catch (InvalidOperationException ex) { return BadRequest(ex.Message); }
        await db.SaveChangesAsync();
        new TableauService(db).EnregistrerResultatCombat(combat);
        audit.Consigner("Combat", id, "Terminé (Hantei)", null, req.Couleur);
        return combat.ToDto();
    }
}
