using System.IO;
using System.Windows;
using FkcScoring.Core.Data;
using Microsoft.EntityFrameworkCore;
using QuestPDF.Infrastructure;

namespace FkcScoring.App;

public partial class App : Application
{
    public static FkcScoringContext Db { get; private set; } = null!;
    public static string DbPath { get; private set; } = "";
    public static string BackupDir { get; private set; } = "";
    public static string ExportDir { get; private set; } = "";

    protected override void OnStartup(StartupEventArgs e)
    {
        base.OnStartup(e);

        QuestPDF.Settings.License = LicenseType.Community;

        var appData = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "FkcScoring");
        Directory.CreateDirectory(appData);
        DbPath = Path.Combine(appData, "fkc_scoring.db");
        BackupDir = Path.Combine(appData, "backups");
        ExportDir = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.MyDocuments), "FKC Scoring Exports");

        var options = new DbContextOptionsBuilder<FkcScoringContext>()
            .UseSqlite($"Data Source={DbPath}")
            .Options;
        Db = new FkcScoringContext(options);

        try
        {
            Db.Database.Migrate();
        }
        catch (Exception ex)
        {
            MessageBox.Show($"Impossible d'ouvrir la base de données locale :\n{ex.Message}", "FKC Scoring — Erreur", MessageBoxButton.OK, MessageBoxImage.Error);
            Shutdown(-1);
        }
    }

    protected override void OnExit(ExitEventArgs e)
    {
        Db.Dispose();
        base.OnExit(e);
    }
}
