using System.Windows;
using System.Windows.Controls;
using FkcScoring.App.Services;
using FkcScoring.Core.Data.Entities;

namespace FkcScoring.App.Views;

public partial class CompetitionView : UserControl
{
    public event EventHandler? CompetitionChangee;

    public CompetitionView()
    {
        InitializeComponent();
        Rafraichir();
    }

    public void Rafraichir()
    {
        var competition = AppState.CompetitionId is int id ? App.Db.Competitions.Find(id) : null;
        if (competition == null)
        {
            AppState.CompetitionId = null;
            PanelCreation.Visibility = Visibility.Visible;
            PanelDetail.Visibility = Visibility.Collapsed;
            return;
        }

        PanelCreation.Visibility = Visibility.Collapsed;
        PanelDetail.Visibility = Visibility.Visible;
        TxtInfoCompetition.Text = $"{competition.Nom} — {competition.Date:dd/MM/yyyy} — {competition.Lieu}";
        TxtInfoParametres.Text = $"Niveau {competition.Niveau} — Règlement {competition.Reglement} — " +
            $"Ippon {competition.PointsIppon} / Waza-ari {competition.PointsWazaAri} / Yuko {competition.PointsYuko} — " +
            $"écart de victoire {competition.EcartVictoire} pts — durée par défaut {competition.DureeCombatDefautSec}s";

        RafraichirCategories(competition.Id);
    }

    private void RafraichirCategories(int competitionId)
    {
        LstCategories.ItemsSource = App.Db.Categories
            .Where(c => c.CompetitionId == competitionId)
            .OrderBy(c => c.Nom)
            .Select(c => $"{c.Nom}  —  {LibelleDiscipline(c.Discipline)}")
            .ToList();
    }

    private static string LibelleDiscipline(Discipline d) => d switch
    {
        Discipline.KumiteIndividuel => "Kumite individuel",
        Discipline.KataIndividuel => "Kata individuel",
        Discipline.KataEquipe => "Kata par équipe",
        _ => d.ToString()
    };

    private void CreerCompetition_Click(object sender, RoutedEventArgs e)
    {
        if (string.IsNullOrWhiteSpace(TxtNom.Text))
        {
            MessageBox.Show("Le nom de la compétition est obligatoire.", "FKC Scoring", MessageBoxButton.OK, MessageBoxImage.Warning);
            return;
        }

        var niveau = ((ComboBoxItem)CmbNiveau.SelectedItem).Content!.ToString() switch
        {
            "Ligue" => NiveauCompetition.Ligue,
            "National" => NiveauCompetition.National,
            _ => NiveauCompetition.Club
        };

        var competition = new Competition
        {
            Nom = TxtNom.Text.Trim(),
            Date = DpDate.SelectedDate ?? DateTime.Today,
            Lieu = TxtLieu.Text.Trim(),
            Niveau = niveau
        };
        App.Db.Competitions.Add(competition);
        App.Db.SaveChanges();

        AppState.CompetitionId = competition.Id;
        Rafraichir();
        CompetitionChangee?.Invoke(this, EventArgs.Empty);
    }

    private void AjouterCategorie_Click(object sender, RoutedEventArgs e)
    {
        if (AppState.CompetitionId is not int competitionId) return;
        if (string.IsNullOrWhiteSpace(TxtNomCategorie.Text))
        {
            MessageBox.Show("Le nom de la catégorie est obligatoire.", "FKC Scoring", MessageBoxButton.OK, MessageBoxImage.Warning);
            return;
        }

        var discipline = ((ComboBoxItem)CmbDiscipline.SelectedItem).Content!.ToString() switch
        {
            "Kata individuel" => Discipline.KataIndividuel,
            "Kata par équipe" => Discipline.KataEquipe,
            _ => Discipline.KumiteIndividuel
        };

        App.Db.Categories.Add(new Categorie
        {
            CompetitionId = competitionId,
            Nom = TxtNomCategorie.Text.Trim(),
            Discipline = discipline
        });
        App.Db.SaveChanges();

        TxtNomCategorie.Clear();
        RafraichirCategories(competitionId);
        CompetitionChangee?.Invoke(this, EventArgs.Empty);
    }
}
