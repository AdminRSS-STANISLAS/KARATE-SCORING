using FkcScoring.Core.Data;
using FkcScoring.Core.Data.Entities;

namespace FkcScoring.Core.Domain;

/// <summary>Journal des modifications de score et décisions (cahier 5.7), pour audit en cas de contestation.</summary>
public class AuditService
{
    private readonly FkcScoringContext _db;

    public AuditService(FkcScoringContext db) => _db = db;

    public void Consigner(string entiteType, int entiteId, string action, string? ancienneValeur = null, string? nouvelleValeur = null, string? utilisateur = null)
    {
        _db.AuditLogs.Add(new AuditLog
        {
            EntiteType = entiteType,
            EntiteId = entiteId,
            Action = action,
            AncienneValeur = ancienneValeur,
            NouvelleValeur = nouvelleValeur,
            Utilisateur = utilisateur,
            Horodatage = DateTime.Now
        });
        _db.SaveChanges();
    }
}
