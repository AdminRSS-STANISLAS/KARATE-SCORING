namespace FkcScoring.Core.Data.Entities;

/// <summary>Journal des modifications de score, pour audit en cas de contestation.</summary>
public class AuditLog
{
    public int Id { get; set; }
    public string EntiteType { get; set; } = "";
    public int EntiteId { get; set; }
    public string Action { get; set; } = "";
    public string? AncienneValeur { get; set; }
    public string? NouvelleValeur { get; set; }
    public string? Utilisateur { get; set; }
    public DateTime Horodatage { get; set; } = DateTime.Now;
}
