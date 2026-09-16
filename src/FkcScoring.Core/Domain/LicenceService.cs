using System.Net.NetworkInformation;
using System.Security.Cryptography;
using System.Text;

namespace FkcScoring.Core.Domain;

/// <summary>
/// Activation logicielle liée au poste (cahier §21 "vendre sans se faire voler le logiciel") : sans clé
/// valide pour CE poste, l'API refuse tout (voir le middleware de licence dans Program.cs) — seul
/// l'écran d'activation reste accessible. La clé est un HMAC de l'empreinte matérielle du poste avec un
/// secret partagé UNIQUEMENT avec <c>tools/LicenceKeygen</c> (jamais distribué, jamais exposé par une
/// route API) : recalculer la même clé sans connaître ce secret n'est pas faisable en pratique.
///
/// Limite honnête : comme tout schéma de licence purement local (pas de serveur d'activation), un
/// attaquant qui décompile le binaire distribué peut retrouver ce secret et forger ses propres clés.
/// Ça dissuade la copie/revente casuelle (l'objectif réel ici), pas un cracker déterminé — aucun schéma
/// offline ne peut faire mieux sans introduire une dépendance réseau, contraire à l'exigence offline-first.
/// </summary>
public static class LicenceService
{
    // Change cette valeur ET regénère tools/LicenceKeygen avec le nouveau secret avant toute première
    // distribution réelle du logiciel — ce fichier étant dans le dépôt (privé), la valeur ci-dessous
    // ne doit être considérée fiable que tant que le dépôt reste privé et le binaire non décompilé.
    private const string Secret = "5bc195ea2ac24a8ef0dab1ba34cb3309e56dd53f924e2dd4";

    /// <summary>Empreinte du poste courant (adresse MAC de la première interface réseau active + nom de
    /// machine), affichée à l'organisateur pour qu'il la transmette au vendeur en échange d'une clé.</summary>
    public static string EmpreinteMachine()
    {
        var mac = AdresseMacPrincipale() ?? "SANS-RESEAU";
        var donnee = $"{mac}|{Environment.MachineName}";
        var hash = SHA256.HashData(Encoding.UTF8.GetBytes(donnee));
        return FormaterParGroupes(Convert.ToHexString(hash)[..12]);
    }

    /// <summary>Calcule la clé d'activation attendue pour une empreinte donnée — utilisé à la fois pour
    /// valider une clé saisie (ci-dessous) et par l'outil de génération privé.</summary>
    public static string GenererCle(string empreinteMachine)
    {
        var hash = HMACSHA256.HashData(Encoding.UTF8.GetBytes(Secret), Encoding.UTF8.GetBytes(Nettoyer(empreinteMachine)));
        return FormaterParGroupes(Convert.ToHexString(hash)[..16]);
    }

    public static bool ValiderCle(string? cle, string empreinteMachine)
    {
        if (string.IsNullOrWhiteSpace(cle)) return false;
        var attendue = GenererCle(empreinteMachine);
        return CryptographicOperations.FixedTimeEquals(
            Encoding.UTF8.GetBytes(Nettoyer(cle)), Encoding.UTF8.GetBytes(Nettoyer(attendue)));
    }

    private static string? AdresseMacPrincipale() => NetworkInterface.GetAllNetworkInterfaces()
        .Where(ni => ni.OperationalStatus == OperationalStatus.Up && ni.NetworkInterfaceType != NetworkInterfaceType.Loopback)
        .Select(ni => ni.GetPhysicalAddress().ToString())
        .FirstOrDefault(s => !string.IsNullOrEmpty(s));

    private static string Nettoyer(string s) => s.Replace("-", "").Trim().ToUpperInvariant();

    private static string FormaterParGroupes(string hex)
    {
        var groupes = Enumerable.Range(0, hex.Length / 4).Select(i => hex.Substring(i * 4, 4));
        return string.Join("-", groupes);
    }
}
