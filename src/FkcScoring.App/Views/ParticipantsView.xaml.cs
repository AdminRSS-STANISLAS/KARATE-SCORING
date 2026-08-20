using System.Windows;
using System.Windows.Controls;
using FkcScoring.App.Services;
using FkcScoring.Core.Data.Entities;

namespace FkcScoring.App.Views;

public partial class ParticipantsView : UserControl
{
    public ParticipantsView()
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

        CmbCategorieInscription.ItemsSource = App.Db.Categories
            .Where(c => c.CompetitionId == competitionId)
            .OrderBy(c => c.Nom)
            .Select(c => new { c.Id, Libelle = c.Nom })
            .ToList();
        CmbCategorieInscription.DisplayMemberPath = "Libelle";
        CmbCategorieInscription.SelectedValuePath = "Id";

        var participants = App.Db.Participants.OrderBy(p => p.Nom).ToList();
        CmbParticipantInscription.ItemsSource = participants
            .Select(p => new { p.Id, Libelle = $"{p.Prenom} {p.Nom}" })
            .ToList();
        CmbParticipantInscription.DisplayMemberPath = "Libelle";
        CmbParticipantInscription.SelectedValuePath = "Id";

        var inscriptions = App.Db.Inscriptions
            .Where(i => i.Categorie!.CompetitionId == competitionId && i.ParticipantId != null)
            .ToList();

        LstParticipants.ItemsSource = participants.Select(p =>
        {
            var categoriesInscrites = string.Join(", ", inscriptions
                .Where(i => i.ParticipantId == p.Id)
                .Select(i => i.Categorie!.Nom));
            return $"{p.Prenom} {p.Nom}  —  {p.Club?.Nom}" +
                (string.IsNullOrEmpty(categoriesInscrites) ? "" : $"  —  inscrit(e) à : {categoriesInscrites}");
        }).ToList();
    }

    private void AjouterParticipant_Click(object sender, RoutedEventArgs e)
    {
        if (string.IsNullOrWhiteSpace(TxtPrenom.Text) || string.IsNullOrWhiteSpace(TxtNom.Text))
        {
            MessageBox.Show("Le prénom et le nom sont obligatoires.", "FKC Scoring", MessageBoxButton.OK, MessageBoxImage.Warning);
            return;
        }

        var nomClub = string.IsNullOrWhiteSpace(TxtClub.Text) ? "Club non renseigné" : TxtClub.Text.Trim();
        var club = App.Db.Clubs.FirstOrDefault(c => c.Nom == nomClub);
        if (club == null)
        {
            club = new Club { Nom = nomClub };
            App.Db.Clubs.Add(club);
            App.Db.SaveChanges();
        }

        App.Db.Participants.Add(new Participant
        {
            Prenom = TxtPrenom.Text.Trim(),
            Nom = TxtNom.Text.Trim(),
            ClubId = club.Id,
            Grade = string.IsNullOrWhiteSpace(TxtGrade.Text) ? null : TxtGrade.Text.Trim(),
            DateNaissance = DpNaissance.SelectedDate,
            Sexe = string.IsNullOrWhiteSpace(TxtSexe.Text) ? null : TxtSexe.Text.Trim().ToUpperInvariant()
        });
        App.Db.SaveChanges();

        TxtPrenom.Clear(); TxtNom.Clear(); TxtClub.Clear(); TxtGrade.Clear(); TxtSexe.Clear(); DpNaissance.SelectedDate = null;
        Rafraichir();
    }

    private void Inscrire_Click(object sender, RoutedEventArgs e)
    {
        if (CmbParticipantInscription.SelectedValue is not int participantId || CmbCategorieInscription.SelectedValue is not int categorieId)
        {
            MessageBox.Show("Sélectionnez un participant et une catégorie.", "FKC Scoring", MessageBoxButton.OK, MessageBoxImage.Warning);
            return;
        }

        var dejaInscrit = App.Db.Inscriptions.Any(i => i.ParticipantId == participantId && i.CategorieId == categorieId);
        if (dejaInscrit)
        {
            MessageBox.Show("Ce participant est déjà inscrit à cette catégorie.", "FKC Scoring", MessageBoxButton.OK, MessageBoxImage.Information);
            return;
        }

        App.Db.Inscriptions.Add(new Inscription { ParticipantId = participantId, CategorieId = categorieId, Statut = StatutInscription.Inscrit });
        App.Db.SaveChanges();
        Rafraichir();
    }
}
