using System.Windows;
using System.Windows.Controls;
using System.Windows.Threading;
using FkcScoring.App.Services;
using FkcScoring.Core.Data.Entities;
using FkcScoring.Core.Domain;
using Microsoft.EntityFrameworkCore;

namespace FkcScoring.App.Views;

public partial class ArbitrageKumiteView : UserControl
{
    private readonly DispatcherTimer _timer = new() { Interval = TimeSpan.FromSeconds(1) };
    private Combat? _combat;
    private TimeSpan _dureeTotale;
    private TimeSpan _tempsRestant;
    private int _ecartVictoire = 8;
    private bool _estEnPoule;
    private string _categorieNom = "";
    private string _clubAka = "";
    private string _clubAo = "";
    private DisplayPublicWindow? _fenetreDiffusion;
    private readonly Dictionary<string, TypePenalite> _penalites = new()
    {
        ["Chukoku (avert. cat.1)"] = TypePenalite.Chukoku,
        ["Keikoku (avert. cat.2)"] = TypePenalite.Keikoku,
        ["Hansoku-chui"] = TypePenalite.HansokuChui,
        ["Hansoku (disqualif.)"] = TypePenalite.Hansoku,
        ["Kiken (forfait)"] = TypePenalite.Kiken,
        ["Shikkaku (faute grave)"] = TypePenalite.Shikkaku
    };

    public ArbitrageKumiteView()
    {
        InitializeComponent();
        CmbPenaliteAka.ItemsSource = _penalites.Keys.ToList();
        CmbPenaliteAo.ItemsSource = _penalites.Keys.ToList();
        _timer.Tick += Timer_Tick;
    }

    public void Rafraichir()
    {
        _timer.Stop();
        if (AppState.CompetitionId is not int competitionId)
        {
            TxtAvertissement.Visibility = Visibility.Visible;
            PanelContenu.Visibility = Visibility.Collapsed;
            return;
        }

        var jouables = App.Db.Combats
            .Where(c => c.Tableau!.Categorie!.CompetitionId == competitionId
                && c.Statut != StatutCombat.Termine && !c.EstBye
                && c.CompetiteurAkaId != null && c.CompetiteurAoId != null)
            .OrderBy(c => c.EstRepechage).ThenBy(c => c.Tour).ThenBy(c => c.Id)
            .ToList();

        if (jouables.Count == 0)
        {
            TxtAvertissement.Visibility = Visibility.Visible;
            TxtAvertissement.Text = "Aucun combat à arbitrer pour l'instant. Générez un tableau (onglet 3), ou tous les combats sont déjà terminés.";
            PanelContenu.Visibility = Visibility.Collapsed;
            return;
        }

        TxtAvertissement.Visibility = Visibility.Collapsed;
        PanelContenu.Visibility = Visibility.Visible;

        var noms = App.Db.Participants
            .Where(p => jouables.Select(c => c.CompetiteurAkaId).Contains(p.Id) || jouables.Select(c => c.CompetiteurAoId).Contains(p.Id))
            .ToDictionary(p => p.Id, p => p.NomComplet);

        var idxAvant = CmbCombat.SelectedIndex;
        CmbCombat.ItemsSource = jouables.Select(c => new
        {
            c.Id,
            Libelle = $"Tour {c.Tour}{(c.EstRepechage ? " (repêchage)" : "")} — {noms.GetValueOrDefault(c.CompetiteurAkaId ?? 0, "?")} vs {noms.GetValueOrDefault(c.CompetiteurAoId ?? 0, "?")}"
        }).ToList();
        CmbCombat.DisplayMemberPath = "Libelle";
        CmbCombat.SelectedValuePath = "Id";
        CmbCombat.SelectedIndex = idxAvant >= 0 && idxAvant < CmbCombat.Items.Count ? idxAvant : 0;
    }

    private void CmbCombat_SelectionChanged(object sender, SelectionChangedEventArgs e)
    {
        _timer.Stop();
        if (CmbCombat.SelectedValue is not int combatId) { _combat = null; return; }

        _combat = App.Db.Combats.Single(c => c.Id == combatId);
        App.Db.Entry(_combat).Collection(c => c.Evenements).Load();
        var tableau = App.Db.Tableaux.Single(t => t.Id == _combat.TableauId);
        var categorie = App.Db.Categories.Single(c => c.Id == tableau.CategorieId);
        var competition = App.Db.Competitions.Single(c => c.Id == categorie.CompetitionId);

        _ecartVictoire = competition.EcartVictoire;
        _estEnPoule = tableau.Format == FormatTableau.PouleUnique;
        _dureeTotale = TimeSpan.FromSeconds(categorie.DureeCombatSec ?? competition.DureeCombatDefautSec);
        _tempsRestant = _dureeTotale;

        var participants = App.Db.Participants
            .Where(p => p.Id == _combat.CompetiteurAkaId || p.Id == _combat.CompetiteurAoId)
            .Include(p => p.Club)
            .ToDictionary(p => p.Id);
        TxtNomAka.Text = participants.GetValueOrDefault(_combat.CompetiteurAkaId ?? 0)?.NomComplet ?? "?";
        TxtNomAo.Text = participants.GetValueOrDefault(_combat.CompetiteurAoId ?? 0)?.NomComplet ?? "?";
        _clubAka = participants.GetValueOrDefault(_combat.CompetiteurAkaId ?? 0)?.Club?.Nom ?? "";
        _clubAo = participants.GetValueOrDefault(_combat.CompetiteurAoId ?? 0)?.Club?.Nom ?? "";
        _categorieNom = categorie.Nom;

        RafraichirAffichage();
    }

    private void Timer_Tick(object? sender, EventArgs e)
    {
        _tempsRestant -= TimeSpan.FromSeconds(1);
        if (_tempsRestant <= TimeSpan.Zero)
        {
            _tempsRestant = TimeSpan.Zero;
            _timer.Stop();
            AppliquerFinDeTemps();
        }
        RafraichirAffichage();
    }

    private void Demarrer_Click(object sender, RoutedEventArgs e)
    {
        if (_combat == null) return;
        if (_combat.Statut == StatutCombat.EnAttente) CombatEngine.DemarrerCombat(_combat);
        _timer.Start();
        RafraichirAffichage();
    }

    private void Pause_Click(object sender, RoutedEventArgs e) => _timer.Stop();

    private void FinDuTemps_Click(object sender, RoutedEventArgs e)
    {
        _timer.Stop();
        AppliquerFinDeTemps();
    }

    private void AppliquerFinDeTemps()
    {
        if (_combat == null || _combat.Statut != StatutCombat.EnCours) return;
        CombatEngine.TerminerParFinDeTemps(_combat, _estEnPoule);
        RafraichirAffichage();
    }

    private void PointAka_Click(object sender, RoutedEventArgs e) => AjouterPoint(Couleur.Aka, (Button)sender);
    private void PointAo_Click(object sender, RoutedEventArgs e) => AjouterPoint(Couleur.Ao, (Button)sender);

    private void AjouterPoint(Couleur couleur, Button bouton)
    {
        if (_combat == null || _combat.Statut != StatutCombat.EnCours)
        {
            MessageBox.Show("Démarrez le chronomètre avant de saisir un point.", "FKC Scoring", MessageBoxButton.OK, MessageBoxImage.Warning);
            return;
        }
        var points = int.Parse((string)bouton.Tag);
        var temps = _dureeTotale - _tempsRestant;
        CombatEngine.AjouterPoint(_combat, couleur, points, temps);
        CombatEngine.VerifierEcartVictoire(_combat, _ecartVictoire);
        if (_combat.Statut == StatutCombat.Termine) _timer.Stop();
        RafraichirAffichage();
    }

    private void PenaliteAka_Click(object sender, RoutedEventArgs e) => AppliquerPenalite(Couleur.Aka, CmbPenaliteAka);
    private void PenaliteAo_Click(object sender, RoutedEventArgs e) => AppliquerPenalite(Couleur.Ao, CmbPenaliteAo);

    private void AppliquerPenalite(Couleur couleur, ComboBox combo)
    {
        if (_combat == null || _combat.Statut != StatutCombat.EnCours)
        {
            MessageBox.Show("Démarrez le chronomètre avant de saisir une pénalité.", "FKC Scoring", MessageBoxButton.OK, MessageBoxImage.Warning);
            return;
        }
        if (combo.SelectedItem is not string libelle || !_penalites.TryGetValue(libelle, out var penalite))
        {
            MessageBox.Show("Sélectionnez une pénalité.", "FKC Scoring", MessageBoxButton.OK, MessageBoxImage.Warning);
            return;
        }
        var temps = _dureeTotale - _tempsRestant;
        CombatEngine.AppliquerPenalite(_combat, couleur, penalite, temps);
        if (_combat.Statut == StatutCombat.Termine) _timer.Stop();
        RafraichirAffichage();
    }

    private void HanteiAka_Click(object sender, RoutedEventArgs e) => AppliquerHantei(Couleur.Aka);
    private void HanteiAo_Click(object sender, RoutedEventArgs e) => AppliquerHantei(Couleur.Ao);

    private void AppliquerHantei(Couleur vainqueur)
    {
        if (_combat == null) return;
        CombatEngine.EnregistrerHantei(_combat, vainqueur);
        RafraichirAffichage();
    }

    private void Enregistrer_Click(object sender, RoutedEventArgs e)
    {
        if (_combat == null || _combat.Statut != StatutCombat.Termine || _combat.VainqueurCouleur == null) return;

        _combat.DureeReelleSec = (int)(_dureeTotale - _tempsRestant).TotalSeconds;
        var service = new TableauService(App.Db);
        service.EnregistrerResultatCombat(_combat);

        MessageBox.Show("Résultat enregistré.", "FKC Scoring", MessageBoxButton.OK, MessageBoxImage.Information);
        Rafraichir();
    }

    private void Diffuser_Click(object sender, RoutedEventArgs e)
    {
        if (_fenetreDiffusion != null)
        {
            _fenetreDiffusion.Activate();
            return;
        }

        if (!EcranService.SecondEcranDisponible)
        {
            MessageBox.Show(
                "Aucun second écran détecté. Branchez la TV (HDMI) puis réessayez — la diffusion s'ouvrira alors en plein écran dessus.\n\nEn l'absence de second écran, elle s'ouvre ici à titre d'aperçu.",
                "FKC Scoring", MessageBoxButton.OK, MessageBoxImage.Information);
        }

        _fenetreDiffusion = new DisplayPublicWindow();
        _fenetreDiffusion.Closed += (_, _) => _fenetreDiffusion = null;
        EcranService.PlacerSurSecondEcran(_fenetreDiffusion);
        _fenetreDiffusion.Show();
        PublierEtatDiffusion();
    }

    private void PublierEtatDiffusion()
    {
        if (_combat == null) return;
        LiveScoreboardState.Publier(new ScoreboardSnapshot
        {
            CombatActif = _combat.Statut != StatutCombat.Termine,
            NomAka = TxtNomAka.Text,
            ClubAka = _clubAka,
            NomAo = TxtNomAo.Text,
            ClubAo = _clubAo,
            ScoreAka = _combat.ScoreAka,
            ScoreAo = _combat.ScoreAo,
            Chrono = TxtChrono.Text,
            ChronoEnCours = _timer.IsEnabled,
            Categorie = _categorieNom,
            EtatCombat = TxtEtatCombat.Text
        });
    }

    private void RafraichirAffichage()
    {
        if (_combat == null) return;

        TxtChrono.Text = _tempsRestant.ToString(@"mm\:ss");
        TxtScoreAka.Text = _combat.ScoreAka.ToString();
        TxtScoreAo.Text = _combat.ScoreAo.ToString();

        var enCours = _combat.Statut == StatutCombat.EnCours;
        var enAttenteHantei = _combat.Statut == StatutCombat.EnCours && _combat.ModeDecision == ModeDecision.Hantei && _tempsRestant == TimeSpan.Zero;
        BtnHanteiAka.Visibility = enAttenteHantei ? Visibility.Visible : Visibility.Collapsed;
        BtnHanteiAo.Visibility = enAttenteHantei ? Visibility.Visible : Visibility.Collapsed;

        var termine = _combat.Statut == StatutCombat.Termine;
        BtnEnregistrer.Visibility = termine ? Visibility.Visible : Visibility.Collapsed;
        BtnDemarrer.IsEnabled = !termine && !enCours;
        BtnPause.IsEnabled = enCours;
        BtnFinDuTemps.IsEnabled = enCours;

        TxtEtatCombat.Text = termine
            ? $"Combat terminé — décision : {LibelleDecision(_combat.ModeDecision)} — vainqueur : {(_combat.VainqueurCouleur == Couleur.Aka ? TxtNomAka.Text : TxtNomAo.Text)}"
            : enAttenteHantei ? "Égalité en fin de temps : décision arbitrale (Hantei) requise."
            : enCours ? "Combat en cours." : "En attente du démarrage.";

        LstHistorique.ItemsSource = _combat.Evenements.OrderBy(ev => ev.TempsCombat).Select(ev => ev.Type == TypeEvenement.Point
            ? $"{ev.TempsCombat:mm\\:ss} — Point ({ev.Points}) pour {ev.Couleur}"
            : $"{ev.TempsCombat:mm\\:ss} — Pénalité {ev.Penalite} pour {ev.Couleur}").ToList();

        PublierEtatDiffusion();
    }

    private static string LibelleDecision(ModeDecision? mode) => mode switch
    {
        ModeDecision.EcartPoints => "écart de points",
        ModeDecision.FinTemps => "fin de temps réglementaire",
        ModeDecision.Hantei => "décision arbitrale (Hantei)",
        ModeDecision.Disqualification => "disqualification",
        _ => "-"
    };
}
