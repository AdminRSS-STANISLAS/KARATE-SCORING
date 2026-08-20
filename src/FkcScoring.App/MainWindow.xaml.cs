using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using System.Windows.Media.Animation;
using FkcScoring.App.Services;
using FkcScoring.App.Views;

namespace FkcScoring.App;

public partial class MainWindow : Window
{
    private readonly CompetitionView _competitionView = new();
    private readonly ParticipantsView _participantsView = new();
    private readonly TableauView _tableauView = new();
    private readonly ArbitrageKumiteView _arbitrageView = new();
    private readonly ExportView _exportView = new();

    private static readonly System.Windows.Media.Brush BrushNavActif = new SolidColorBrush(Color.FromRgb(0xE4, 0x00, 0x2B));
    private static readonly System.Windows.Media.Brush BrushNavInactif = new SolidColorBrush(Color.FromRgb(0x16, 0x16, 0x16));

    public MainWindow()
    {
        InitializeComponent();
        _competitionView.CompetitionChangee += (_, _) => RafraichirEnTete();
        _exportView.ExportationTerminee += (_, _) => { RafraichirEnTete(); NaviguerVers(_competitionView, BtnCompetition); };

        RafraichirEnTete();
        NaviguerVers(_competitionView, BtnCompetition);
    }

    private void RafraichirEnTete()
    {
        var competition = AppState.CompetitionId is int id ? App.Db.Competitions.Find(id) : null;
        TxtCompetitionCourante.Text = competition == null ? "Aucune compétition" : $"{competition.Nom}\n{competition.Date:dd/MM/yyyy}";
    }

    /// <summary>Affiche l'écran demandé avec un fondu d'entrée, et met en surbrillance le bouton de nav actif.</summary>
    private void NaviguerVers(UserControl vue, Button boutonActif)
    {
        MainContent.Content = vue;

        foreach (var btn in new[] { BtnCompetition, BtnParticipants, BtnTableau, BtnArbitrage, BtnExport })
            btn.Background = btn == boutonActif ? BrushNavActif : BrushNavInactif;

        MainContent.Opacity = 0;
        var fondu = new DoubleAnimation(0, 1, TimeSpan.FromMilliseconds(220)) { EasingFunction = new QuadraticEase { EasingMode = EasingMode.EaseOut } };
        MainContent.BeginAnimation(OpacityProperty, fondu);
    }

    private void NavCompetition_Click(object sender, RoutedEventArgs e)
    {
        _competitionView.Rafraichir();
        NaviguerVers(_competitionView, BtnCompetition);
    }

    private void NavParticipants_Click(object sender, RoutedEventArgs e)
    {
        _participantsView.Rafraichir();
        NaviguerVers(_participantsView, BtnParticipants);
    }

    private void NavTableau_Click(object sender, RoutedEventArgs e)
    {
        _tableauView.Rafraichir();
        NaviguerVers(_tableauView, BtnTableau);
    }

    private void NavArbitrage_Click(object sender, RoutedEventArgs e)
    {
        _arbitrageView.Rafraichir();
        NaviguerVers(_arbitrageView, BtnArbitrage);
    }

    private void NavExport_Click(object sender, RoutedEventArgs e)
    {
        NaviguerVers(_exportView, BtnExport);
    }
}
