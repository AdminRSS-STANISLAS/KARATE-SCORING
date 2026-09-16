using System.Diagnostics;
using System.IO;
using System.Net.Http;

namespace KarateScoring.Desktop.Services;

/// <summary>
/// Démarre KarateScoring.Api comme processus enfant (pas en hébergement in-process) : la coque
/// desktop reste une fine fenêtre WebView2 par-dessus le moteur web existant, sans rien réécrire — le
/// même exécutable API reste utilisable seul en ligne de commande (cahier DEPLOYMENT.md) et continue de
/// servir les postes tatami sur le réseau local exactement comme avant.
/// </summary>
public class ApiHostLauncher : IDisposable
{
    public const int Port = 5090;
    public string BaseUrl => $"http://localhost:{Port}";

    private Process? _process;

    public async Task<bool> DemarrerEtAttendreAsync(string dossierDonnees, TimeSpan timeout)
    {
        var cheminExe = LocaliserApiExe();
        var psi = cheminExe != null
            ? new ProcessStartInfo(cheminExe) { WorkingDirectory = Path.GetDirectoryName(cheminExe) }
            : LocaliserApiEnModeDev();
        if (psi == null) return false;

        psi.UseShellExecute = false;
        psi.CreateNoWindow = true;
        psi.RedirectStandardOutput = true;
        psi.RedirectStandardError = true;
        psi.EnvironmentVariables["KARATE_SCORING_DB_PATH"] = Path.Combine(dossierDonnees, "karate-scoring.db");
        psi.EnvironmentVariables["KARATE_SCORING_URLS"] = $"http://127.0.0.1:{Port}";

        _process = Process.Start(psi);
        if (_process == null) return false;

        using var http = new HttpClient { Timeout = TimeSpan.FromSeconds(2) };
        var limite = DateTime.UtcNow + timeout;
        while (DateTime.UtcNow < limite)
        {
            if (_process.HasExited) return false;
            try
            {
                var rep = await http.GetAsync($"{BaseUrl}/api/network-info");
                if (rep.IsSuccessStatusCode) return true;
            }
            catch { /* pas encore prêt — on retente */ }
            await Task.Delay(300);
        }
        return false;
    }

    /// <summary>Emplacement de production : "api/KarateScoring.Api.exe" à côté de l'exécutable de la coque (voir installer/setup.iss).</summary>
    private static string? LocaliserApiExe()
    {
        var chemin = Path.Combine(AppContext.BaseDirectory, "api", "KarateScoring.Api.exe");
        return File.Exists(chemin) ? chemin : null;
    }

    /// <summary>Repli développement : exécute l'API depuis les sources via `dotnet run`, pour tester la coque sans publier au préalable.</summary>
    private static ProcessStartInfo? LocaliserApiEnModeDev()
    {
        var dir = new DirectoryInfo(AppContext.BaseDirectory);
        while (dir != null)
        {
            var projet = Path.Combine(dir.FullName, "src", "KarateScoring.Api", "KarateScoring.Api.csproj");
            if (File.Exists(projet))
                return new ProcessStartInfo("dotnet", $"run --project \"{projet}\" --configuration Release --no-launch-profile");
            dir = dir.Parent;
        }
        return null;
    }

    public void Dispose()
    {
        if (_process != null && !_process.HasExited)
        {
            try { _process.Kill(entireProcessTree: true); } catch { /* déjà arrêté */ }
        }
        _process?.Dispose();
    }
}
