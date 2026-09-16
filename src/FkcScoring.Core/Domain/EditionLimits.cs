namespace FkcScoring.Core.Domain;

/// <summary>
/// Limites de l'édition actuellement distribuée. Un seul point de bascule pour toute la plateforme —
/// le jour où une vraie activation par licence existe, ces constantes deviennent des propriétés lues
/// depuis la licence active plutôt que des valeurs figées. Vérifié côté serveur (pas seulement grisé
/// dans l'interface) : un appel direct à l'API contourne le grisage visuel, pas ce contrôle-ci.
/// </summary>
public static class EditionLimits
{
    public const bool NiveauNationalAutorise = false;
    public const bool KataEquipeAutorise = false;
}
