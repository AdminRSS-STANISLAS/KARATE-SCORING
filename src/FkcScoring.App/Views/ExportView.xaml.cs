using System.IO;
using System.Windows;
using System.Windows.Controls;
using FkcScoring.App.Services;
using FkcScoring.Core.Data.Entities;
using FkcScoring.Core.Domain;
using FkcScoring.Core.Export;
using Microsoft.EntityFrameworkCore;

namespace FkcScoring.App.Views;

public partial class ExportView : UserControl
{
    public event EventHandler? ExportationTerminee;

    public ExportView()
    {
        InitializeComponent();
    }

    private void Exporter_Click(object sender, RoutedEventArgs e)
    {
        if (AppState.CompetitionId is not int competitionId)
        {
            MessageBox.Show("Aucune compétition en cours.", "FKC Scoring", MessageBoxButton.OK, MessageBoxImage.Warning);
            return;
        }

        var confirmation = MessageBox.Show(
            "Ceci va générer les rapports (PDF + Excel), sauvegarder la base actuelle, PUIS vider toutes les données de la compétition en cours pour repartir sur une base propre.\n\nUne copie de sauvegarde horodatée sera conservée. Continuer ?",
            "FKC Scoring — Confirmer l'export",
            MessageBoxButton.YesNo, MessageBoxImage.Question);
        if (confirmation != MessageBoxResult.Yes) return;

        try
        {
            var competition = App.Db.Competitions.Single(c => c.Id == competitionId);

            var combats = App.Db.Combats
                .Include(c => c.CompetiteurAka).ThenInclude(p => p!.Club)
                .Include(c => c.CompetiteurAo).ThenInclude(p => p!.Club)
                .Include(c => c.Evenements)
                .Where(c => c.Tableau!.Categorie!.CompetitionId == competitionId && c.Statut == StatutCombat.Termine)
                .OrderBy(c => c.Tour)
                .ToList();

            var calculator = new ClassementCalculator(App.Db);
            var resultatsParCategorie = new List<(Categorie, List<Classement>)>();
            foreach (var categorie in App.Db.Categories.Where(c => c.CompetitionId == competitionId).ToList())
            {
                var tableau = App.Db.Tableaux.Where(t => t.CategorieId == categorie.Id).OrderByDescending(t => t.Id).FirstOrDefault();
                var classement = tableau != null ? calculator.CalculerEtEnregistrer(tableau.Id) : new List<Classement>();
                if (classement.Count > 0)
                {
                    foreach (var c in classement)
                    {
                        c.Participant = c.ParticipantId != null ? App.Db.Participants.Include(p => p.Club).First(p => p.Id == c.ParticipantId) : null;
                    }
                }
                resultatsParCategorie.Add((categorie, classement));
            }

            var pdf = new KumiteReportBuilder().Generer(competition, combats);
            var excel = new ResultatsExcelExporter().Generer(competition, resultatsParCategorie);

            Directory.CreateDirectory(App.ExportDir);
            var horodatage = DateTime.Now.ToString("yyyyMMdd_HHmmss");
            var nomBase = System.Text.RegularExpressions.Regex.Replace(competition.Nom, "[^a-zA-Z0-9]+", "_");
            var pdfPath = Path.Combine(App.ExportDir, $"{nomBase}_{horodatage}_Kumite.pdf");
            var excelPath = Path.Combine(App.ExportDir, $"{nomBase}_{horodatage}_Resultats.xlsx");
            File.WriteAllBytes(pdfPath, pdf);
            File.WriteAllBytes(excelPath, excel);

            var resetService = new DatabaseResetService(App.DbPath, App.BackupDir);
            string backupPath;
            try
            {
                backupPath = resetService.SauvegarderBaseVersFichier();
            }
            catch (Exception ex)
            {
                MessageBox.Show(
                    $"Les rapports ont été générés, mais la sauvegarde de la base a échoué : {ex.Message}\n\nLa base N'A PAS été vidée par sécurité.",
                    "FKC Scoring — Erreur", MessageBoxButton.OK, MessageBoxImage.Error);
                return;
            }

            resetService.ViderCompetition(App.Db);
            AppState.CompetitionId = null;

            TxtResultat.Text = $"Export terminé.\n\nRapport Kumite : {pdfPath}\nRésultats Excel : {excelPath}\nSauvegarde de la base : {backupPath}\n\nLa base est maintenant vide, prête pour une nouvelle compétition.";
            MessageBox.Show("Export terminé et base réinitialisée. Voir le détail à l'écran.", "FKC Scoring", MessageBoxButton.OK, MessageBoxImage.Information);
            ExportationTerminee?.Invoke(this, EventArgs.Empty);
        }
        catch (Exception ex)
        {
            MessageBox.Show($"Erreur pendant l'export : {ex.Message}", "FKC Scoring — Erreur", MessageBoxButton.OK, MessageBoxImage.Error);
        }
    }
}
