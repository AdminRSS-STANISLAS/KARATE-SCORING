namespace FkcScoring.Core.Data.Entities;

/// <summary>
/// Réglages machine (pas liés à une compétition), une seule ligne (Id = 1).
/// </summary>
public class Parametres
{
    public int Id { get; set; }

    /// <summary>Hash PBKDF2 du code administrateur (format "iterations.saltBase64.hashBase64"), null si non configuré.</summary>
    public string? AdminCodeHash { get; set; }
}
