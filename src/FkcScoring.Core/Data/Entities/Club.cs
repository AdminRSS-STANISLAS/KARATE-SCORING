namespace FkcScoring.Core.Data.Entities;

public class Club
{
    public int Id { get; set; }
    public string Nom { get; set; } = "";
    public string? Ville { get; set; }

    /// <summary>"png" ou "jpg" si un logo a été importé (voir <see cref="Domain.ImageUploadService"/>) ; null sinon.</summary>
    public string? LogoExtension { get; set; }
}
