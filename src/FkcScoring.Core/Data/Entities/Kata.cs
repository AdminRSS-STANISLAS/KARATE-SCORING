namespace FkcScoring.Core.Data.Entities;

/// <summary>Référentiel des katas proposés à la saisie (personnalisable par l'organisateur).</summary>
public class Kata
{
    public int Id { get; set; }
    public string Nom { get; set; } = "";
    public bool Actif { get; set; } = true;
}
