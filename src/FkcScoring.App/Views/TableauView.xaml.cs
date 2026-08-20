using System.Windows;
using System.Windows.Controls;
using FkcScoring.App.Services;
using FkcScoring.Core.Data.Entities;
using FkcScoring.Core.Domain;

namespace FkcScoring.App.Views;

public partial class TableauView : UserControl
{
    public TableauView()
    {
        InitializeComponent();
    }

    public void Rafraichir()
    {
        if (AppState.CompetitionId is not int competitionId)
        {
            TxtAvertissement.Visibility = Visibility.Visible;
            PanelContenu.Visibility = Visibility.Collapsed;
            return;
        }

        TxtAvertissement.Visibility = Visibility.Collapsed;
        PanelContenu.Visibility = Visibility.Visible;

        var selectionPrecedente = CmbCategorie.SelectedValue as int?;
        CmbCategorie.ItemsSource = App.Db.Categories
            .Where(c => c.CompetitionId == competitionId && c.Discipline == Discipline.KumiteIndividuel)
            .OrderBy(c => c.Nom)
            .Select(c => new { c.Id, Libelle = c.Nom })
            .ToList();
        CmbCategorie.DisplayMemberPath = "Libelle";
        CmbCategorie.SelectedValuePath = "Id";
        if (selectionPrecedente != null) CmbCategorie.SelectedValue = selectionPrecedente;
        else if (CmbCategorie.Items.Count > 0) CmbCategorie.SelectedIndex = 0;

        AfficherTableau();
    }

    private void CmbCategorie_SelectionChanged(object sender, SelectionChangedEventArgs e) => AfficherTableau();

    private void AfficherTableau()
    {
        if (CmbCategorie.SelectedValue is not int categorieId)
        {
            TxtInfoTableau.Text = "";
            LstCombats.ItemsSource = null;
            BtnGenerer.Visibility = Visibility.Visible;
            BtnPhaseFinale.Visibility = Visibility.Collapsed;
            return;
        }

        var nbInscrits = App.Db.Inscriptions.Count(i => i.CategorieId == categorieId && i.ParticipantId != null);
        var tableau = App.Db.Tableaux
            .Where(t => t.CategorieId == categorieId)
            .OrderByDescending(t => t.Id)
            .FirstOrDefault();

        if (tableau == null)
        {
            TxtInfoTableau.Text = $"{nbInscrits} participant(s) inscrit(s) — aucun tableau généré.";
            BtnGenerer.Visibility = Visibility.Visible;
            BtnGenerer.IsEnabled = nbInscrits >= 2;
            BtnPhaseFinale.Visibility = Visibility.Collapsed;
            LstCombats.ItemsSource = null;
            return;
        }

        BtnGenerer.Visibility = Visibility.Collapsed;
        var combats = App.Db.Combats
            .Where(c => c.TableauId == tableau.Id)
            .OrderBy(c => c.EstRepechage).ThenBy(c => c.Tour).ThenBy(c => c.Id)
            .ToList();

        var participantsIds = combats.SelectMany(c => new[] { c.CompetiteurAkaId, c.CompetiteurAoId }).Where(id => id != null).Select(id => id!.Value).Distinct().ToList();
        var noms = App.Db.Participants.Where(p => participantsIds.Contains(p.Id)).ToDictionary(p => p.Id, p => p.NomComplet);
        string Nom(int? id) => id == null ? "?" : noms.GetValueOrDefault(id.Value, "?");

        TxtInfoTableau.Text = $"Format : {LibelleFormat(tableau.Format)} — {nbInscrits} participant(s).";
        LstCombats.ItemsSource = combats.Select(c =>
        {
            var prefixe = c.EstRepechage ? $"[Repêchage moitié {c.Moitie}] " : "";
            var resultat = c.Statut == StatutCombat.Termine
                ? $"  →  Vainqueur : {(c.VainqueurCouleur == Couleur.Aka ? Nom(c.CompetiteurAkaId) : Nom(c.CompetiteurAoId))}" + (c.EstBye ? " (exempt)" : "")
                : "  →  à jouer";
            return $"{prefixe}Tour {c.Tour} — {Nom(c.CompetiteurAkaId)} (Aka) vs {Nom(c.CompetiteurAoId)} (Ao){resultat}";
        }).ToList();

        // Bouton phase finale pour les tableaux "poule(s) puis élimination", visible dès que les
        // poules (tour 1, non-repêchage) sont toutes terminées et que la phase suivante n'existe pas encore.
        var pouleTerminee = tableau.Format == FormatTableau.PoulePuisElimination
            && combats.Where(c => c.Tour == 1).All(c => c.Statut == StatutCombat.Termine)
            && combats.All(c => c.Tour < 2);
        BtnPhaseFinale.Visibility = pouleTerminee ? Visibility.Visible : Visibility.Collapsed;
    }

    private static string LibelleFormat(FormatTableau f) => f switch
    {
        FormatTableau.FinaleDirecte => "Finale directe",
        FormatTableau.PouleUnique => "Poule unique",
        FormatTableau.PoulePuisElimination => "Poule(s) puis élimination",
        FormatTableau.EliminationRepechage => "Élimination directe + repêchage",
        _ => f.ToString()
    };

    private void Generer_Click(object sender, RoutedEventArgs e)
    {
        if (CmbCategorie.SelectedValue is not int categorieId) return;

        var participantIds = App.Db.Inscriptions
            .Where(i => i.CategorieId == categorieId && i.ParticipantId != null)
            .Select(i => i.ParticipantId!.Value)
            .ToList();

        if (participantIds.Count < 2)
        {
            MessageBox.Show("Il faut au moins 2 participants inscrits pour générer un tableau.", "FKC Scoring", MessageBoxButton.OK, MessageBoxImage.Warning);
            return;
        }

        var service = new TableauService(App.Db);
        service.GenererTableau(categorieId, participantIds);
        AfficherTableau();
    }

    private void GenererPhaseFinale_Click(object sender, RoutedEventArgs e)
    {
        if (CmbCategorie.SelectedValue is not int categorieId) return;
        var tableau = App.Db.Tableaux.Where(t => t.CategorieId == categorieId).OrderByDescending(t => t.Id).First();

        var service = new TableauService(App.Db);
        service.GenererPhaseEliminationApresPoules(tableau.Id);
        AfficherTableau();
    }
}
