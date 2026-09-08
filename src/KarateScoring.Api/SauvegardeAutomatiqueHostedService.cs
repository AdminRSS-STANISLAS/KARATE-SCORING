using FkcScoring.Core.Data;
using FkcScoring.Core.Export;

namespace KarateScoring.Api;

/// <summary>
/// Prend une sauvegarde cohérente de la base à intervalle régulier pendant que l'application tourne
/// (cahier §25) — jusqu'ici, la seule sauvegarde existante n'était prise que juste avant un reset
/// complet (Phase 2), donc absente si le poste plante sans qu'un reset n'ait jamais été déclenché.
/// </summary>
public class SauvegardeAutomatiqueHostedService(IServiceScopeFactory scopeFactory) : BackgroundService
{
    private const string Prefixe = "auto";
    private const int NbAConserver = 20;

    private static TimeSpan Intervalle =>
        TimeSpan.FromMinutes(int.TryParse(Environment.GetEnvironmentVariable("KARATE_SCORING_SAUVEGARDE_INTERVALLE_MIN"), out var m) && m > 0 ? m : 15);

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        var dbPath = FkcScoringPaths.ResolveDbPath();
        var backupDir = FkcScoringPaths.ResolveBackupDir(dbPath);

        while (!stoppingToken.IsCancellationRequested)
        {
            try
            {
                await Task.Delay(Intervalle, stoppingToken);
                using var scope = scopeFactory.CreateScope();
                var db = scope.ServiceProvider.GetRequiredService<FkcScoringContext>();
                SauvegardeService.SauvegarderVersFichier(db, backupDir, Prefixe);
                SauvegardeService.PurgerAnciennes(backupDir, Prefixe, NbAConserver);
            }
            catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested)
            {
                break;
            }
            catch (Exception)
            {
                // Une sauvegarde automatique manquée ne doit jamais interrompre le service ni l'API —
                // on retentera au prochain intervalle.
            }
        }
    }
}
