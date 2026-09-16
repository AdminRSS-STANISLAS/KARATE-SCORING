using System.IO;
using System.Windows;
using System.Windows.Media.Animation;
using KarateScoring.Desktop.Services;
using Microsoft.Win32;

namespace KarateScoring.Desktop;

public partial class SplashWindow : Window
{
    // "même de 5 secondes" — durée minimale d'affichage de l'animation, quel que soit le temps réel de
    // démarrage du serveur local (qui peut être plus rapide, auquel cas on patiente ; ou plus lent, auquel
    // cas l'écran de démarrage reste affiché le temps qu'il faut plutôt que de basculer prématurément).
    private static readonly TimeSpan DureeMinimale = TimeSpan.FromSeconds(5);

    public SplashWindow()
    {
        InitializeComponent();
        Loaded += async (_, _) => await DemarrerAsync();
    }

    private async Task DemarrerAsync()
    {
        AnimerEntree();
        var chrono = System.Diagnostics.Stopwatch.StartNew();

        var dossier = DataFolderSettings.LireDossierConfigure();
        if (dossier == null)
        {
            await Task.Delay(700); // laisse l'animation se jouer avant d'interrompre avec la boîte de dialogue
            dossier = ChoisirDossierDonnees();
            if (dossier == null) { Application.Current.Shutdown(); return; }
            DataFolderSettings.Enregistrer(dossier);
        }
        Directory.CreateDirectory(dossier);

        StatusText.Text = "Démarrage du serveur local…";
        var app = (App)Application.Current;
        var pret = await app.ApiHost.DemarrerEtAttendreAsync(dossier, TimeSpan.FromSeconds(25));

        var attenteRestante = DureeMinimale - chrono.Elapsed;
        if (attenteRestante > TimeSpan.Zero) await Task.Delay(attenteRestante);

        if (!pret)
        {
            MessageBox.Show(this,
                "Impossible de démarrer le serveur local de Karate Scoring. Vérifiez qu'aucune autre instance n'est déjà en cours d'exécution, puis relancez l'application.",
                "Karate Scoring — Erreur de démarrage", MessageBoxButton.OK, MessageBoxImage.Error);
            Application.Current.Shutdown();
            return;
        }

        var main = new MainWindow(app.ApiHost.BaseUrl);
        Application.Current.MainWindow = main;
        Application.Current.ShutdownMode = ShutdownMode.OnMainWindowClose;
        main.Show();
        Close();
    }

    private string? ChoisirDossierDonnees()
    {
        var dialog = new OpenFolderDialog
        {
            Title = "Où enregistrer les données de Karate Scoring ? (compétitions, résultats, sauvegardes)",
            InitialDirectory = DataFolderSettings.SuggestionParDefaut(),
            Multiselect = false,
        };
        return dialog.ShowDialog(this) == true ? dialog.FolderName : null;
    }

    private void AnimerEntree()
    {
        var fadeLogo = new DoubleAnimation(0, 1, TimeSpan.FromMilliseconds(600)) { EasingFunction = new QuadraticEase { EasingMode = EasingMode.EaseOut } };
        var scaleLogo = new DoubleAnimation(0.92, 1.0, TimeSpan.FromMilliseconds(600)) { EasingFunction = new QuadraticEase { EasingMode = EasingMode.EaseOut } };
        Logo.BeginAnimation(OpacityProperty, fadeLogo);
        LogoScale.BeginAnimation(System.Windows.Media.ScaleTransform.ScaleXProperty, scaleLogo);
        LogoScale.BeginAnimation(System.Windows.Media.ScaleTransform.ScaleYProperty, scaleLogo);

        var fadeSub = new DoubleAnimation(0, 1, TimeSpan.FromMilliseconds(500)) { BeginTime = TimeSpan.FromMilliseconds(350) };
        var shiftSub = new DoubleAnimation(8, 0, TimeSpan.FromMilliseconds(500)) { BeginTime = TimeSpan.FromMilliseconds(350), EasingFunction = new QuadraticEase { EasingMode = EasingMode.EaseOut } };
        Subtitle.BeginAnimation(OpacityProperty, fadeSub);
        SubtitleShift.BeginAnimation(System.Windows.Media.TranslateTransform.YProperty, shiftSub);

        var fadeCredit = new DoubleAnimation(0, 1, TimeSpan.FromMilliseconds(500)) { BeginTime = TimeSpan.FromMilliseconds(650) };
        var shiftCredit = new DoubleAnimation(8, 0, TimeSpan.FromMilliseconds(500)) { BeginTime = TimeSpan.FromMilliseconds(650), EasingFunction = new QuadraticEase { EasingMode = EasingMode.EaseOut } };
        Credit.BeginAnimation(OpacityProperty, fadeCredit);
        CreditShift.BeginAnimation(System.Windows.Media.TranslateTransform.YProperty, shiftCredit);
    }
}
