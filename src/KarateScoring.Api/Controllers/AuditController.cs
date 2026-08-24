using FkcScoring.Core.Data;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace KarateScoring.Api.Controllers;

[ApiController]
[Route("api/audit")]
public class AuditController(FkcScoringContext db) : ControllerBase
{
    [HttpGet]
    public async Task<List<AuditDto>> GetAll([FromQuery] int take = 300) =>
        (await db.AuditLogs.OrderByDescending(a => a.Horodatage).Take(Math.Clamp(take, 1, 1000)).ToListAsync())
        .Select(a => new AuditDto(a.Horodatage, a.EntiteType, a.EntiteId, a.Action, a.AncienneValeur, a.NouvelleValeur))
        .ToList();
}
