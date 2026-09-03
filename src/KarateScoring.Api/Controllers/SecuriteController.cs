using FkcScoring.Core.Data;
using FkcScoring.Core.Domain;
using Microsoft.AspNetCore.Mvc;

namespace KarateScoring.Api.Controllers;

[ApiController]
[Route("api/securite")]
public class SecuriteController(FkcScoringContext db) : ControllerBase
{
    [HttpGet]
    public async Task<ActionResult<SecuriteStatusDto>> Get() =>
        new SecuriteStatusDto(await SecuriteService.EstConfigureAsync(db));

    [HttpPost("code")]
    public async Task<IActionResult> DefinirCode(DefinirCodeRequest req)
    {
        if (string.IsNullOrWhiteSpace(req.NouveauCode) || req.NouveauCode.Length < 4)
            return BadRequest("Le code doit contenir au moins 4 caractères.");

        var ok = await SecuriteService.DefinirCodeAsync(db, req.NouveauCode, req.AncienCode);
        if (!ok) return StatusCode(StatusCodes.Status403Forbidden, new { detail = "Ancien code incorrect." });
        return NoContent();
    }
}
