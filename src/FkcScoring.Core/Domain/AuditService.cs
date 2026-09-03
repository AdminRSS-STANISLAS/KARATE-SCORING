using FkcScoring.Core.Data;
using FkcScoring.Core.Data.Entities;
using Microsoft.AspNetCore.Http;

namespace FkcScoring.Core.Domain;

/// <summary>Journal des modifications de score et décisions (cahier 5.7), pour audit en cas de contestation.</summary>
public class AuditService
{
    private readonly FkcScoringContext _db;
    private readonly IHttpContextAccessor _httpContextAccessor;

    public AuditService(FkcScoringContext db, IHttpContextAccessor httpContextAccessor)
    {
        _db = db;
        _httpContextAccessor = httpContextAccessor;
    }

    /// <summary>
    /// <paramref name="utilisateur"/> non fourni : retombe sur l'en-tête X-Operateur de la requête en
    /// cours (nom d'opérateur saisi une fois par appareil côté frontend) — évite de faire passer ce
    /// paramètre à chacun des appels existants dans les contrôleurs.
    /// </summary>
    public void Consigner(string entiteType, int entiteId, string action, string? ancienneValeur = null, string? nouvelleValeur = null, string? utilisateur = null)
    {
        _db.AuditLogs.Add(new AuditLog
        {
            EntiteType = entiteType,
            EntiteId = entiteId,
            Action = action,
            AncienneValeur = ancienneValeur,
            NouvelleValeur = nouvelleValeur,
            Utilisateur = utilisateur ?? OperateurCourant(),
            Horodatage = DateTime.Now
        });
        _db.SaveChanges();
    }

    private string? OperateurCourant()
    {
        var valeur = _httpContextAccessor.HttpContext?.Request.Headers["X-Operateur"].ToString();
        return string.IsNullOrWhiteSpace(valeur) ? null : valeur;
    }
}
