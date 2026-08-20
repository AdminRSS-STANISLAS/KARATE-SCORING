using System.Windows;
using System.Windows.Input;
using System.Windows.Media.Animation;
using FkcScoring.App.Services;

namespace FkcScoring.App.Views;

/// <summary>
/// Fenêtre plein écran destinée à la TV branchée en HDMI : reflète en temps réel ce que l'arbitre
/// saisit dans l'écran d'arbitrage (LiveScoreboardState), toujours avec les scores en très grand.
/// </summary>
public partial class DisplayPublicWindow : Window
{
    private int _dernierScoreAka;
    private int _dernierScoreAo;

    public DisplayPublicWindow()
    {
        InitializeComponent();
        Loaded += (_, _) => Rafraichir(LiveScoreboardState.Actuel, animer: false);
        LiveScoreboardState.Changed += OnChanged;
        Closed += (_, _) => LiveScoreboardState.Changed -= OnChanged;
    }

    private void OnChanged(object? sender, EventArgs e) => Dispatcher.Invoke(() => Rafraichir(LiveScoreboardState.Actuel, animer: true));

    private void Rafraichir(ScoreboardSnapshot etat, bool animer)
    {
        TxtCategorie.Text = etat.Categorie ?? "En attente d'un combat…";
        TxtNomAka.Text = string.IsNullOrWhiteSpace(etat.NomAka) ? "—" : etat.NomAka;
        TxtClubAka.Text = etat.ClubAka;
        TxtNomAo.Text = string.IsNullOrWhiteSpace(etat.NomAo) ? "—" : etat.NomAo;
        TxtClubAo.Text = etat.ClubAo;
        TxtChrono.Text = etat.Chrono;
        TxtEtat.Text = etat.EtatCombat ?? "";

        if (animer && etat.ScoreAka != _dernierScoreAka) PulserScore(EchelleScoreAka);
        if (animer && etat.ScoreAo != _dernierScoreAo) PulserScore(EchelleScoreAo);
        _dernierScoreAka = etat.ScoreAka;
        _dernierScoreAo = etat.ScoreAo;

        TxtScoreAka.Text = etat.ScoreAka.ToString();
        TxtScoreAo.Text = etat.ScoreAo.ToString();
    }

    private static void PulserScore(System.Windows.Media.ScaleTransform echelle)
    {
        var animation = new DoubleAnimation
        {
            From = 1.35,
            To = 1.0,
            Duration = TimeSpan.FromMilliseconds(400),
            EasingFunction = new BackEase { EasingMode = EasingMode.EaseOut, Amplitude = 0.3 }
        };
        echelle.BeginAnimation(System.Windows.Media.ScaleTransform.ScaleXProperty, animation);
        echelle.BeginAnimation(System.Windows.Media.ScaleTransform.ScaleYProperty, animation);
    }

    private void Window_KeyDown(object sender, KeyEventArgs e)
    {
        if (e.Key == Key.Escape) Close();
    }
}
