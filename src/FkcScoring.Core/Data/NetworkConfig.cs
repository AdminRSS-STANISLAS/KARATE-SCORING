using System.Net.NetworkInformation;
using System.Net.Sockets;

namespace FkcScoring.Core.Data;

/// <summary>
/// Résout l'adresse d'écoute Kestrel et les adresses IPv4 locales du poste, pour que l'application
/// soit joignable depuis les postes de scoring des autres tatamis sur le réseau local (aucune
/// connexion internet requise). <c>KARATE_SCORING_URLS</c> permet de surcharger le port/l'interface,
/// même logique que <see cref="FkcScoringPaths.ResolveDbPath"/> pour KARATE_SCORING_DB_PATH.
/// </summary>
public static class NetworkConfig
{
    public const int DefaultPort = 5090;

    public static string ResolveUrls() =>
        Environment.GetEnvironmentVariable("KARATE_SCORING_URLS") ?? $"http://0.0.0.0:{DefaultPort}";

    /// <summary>Adresses IPv4 locales (hors loopback) sur lesquelles ce poste est joignable — à donner aux postes tatami.</summary>
    public static List<string> LocalIPv4Addresses()
    {
        var result = new List<string>();
        foreach (var ni in NetworkInterface.GetAllNetworkInterfaces())
        {
            if (ni.OperationalStatus != OperationalStatus.Up) continue;
            if (ni.NetworkInterfaceType == NetworkInterfaceType.Loopback) continue;
            foreach (var addr in ni.GetIPProperties().UnicastAddresses)
            {
                if (addr.Address.AddressFamily != AddressFamily.InterNetwork) continue;
                var bytes = addr.Address.GetAddressBytes();
                // 169.254.x.x (APIPA/link-local) n'est jamais joignable depuis un autre poste — l'afficher
                // à l'organisateur n'aiderait pas à trouver la bonne adresse pour les postes tatami.
                if (bytes[0] == 169 && bytes[1] == 254) continue;
                result.Add(addr.Address.ToString());
            }
        }
        return result;
    }
}
