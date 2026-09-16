namespace FkcScoring.Core.Data.Entities;

/// <summary>
/// Réglages machine (pas liés à une compétition), une seule ligne (Id = 1).
/// </summary>
public class Parametres
{
    public int Id { get; set; }

    /// <summary>Hash PBKDF2 du code administrateur (format "iterations.saltBase64.hashBase64"), null si non configuré.</summary>
    public string? AdminCodeHash { get; set; }

    /// <summary>Clé d'activation entrée par l'organisateur (voir <see cref="Domain.LicenceService"/>) ;
    /// null tant que le poste n'est pas activé. Revalidée à chaque démarrage contre l'empreinte
    /// matérielle du poste courant — une base copiée sur une autre machine n'y reste pas valide.</summary>
    public string? LicenceCle { get; set; }
}
