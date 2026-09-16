using FkcScoring.Core.Data;
using FkcScoring.Core.Data.Entities;
using FkcScoring.Core.Domain;
using QuestPDF.Fluent;
using QuestPDF.Helpers;
using QuestPDF.Infrastructure;

namespace FkcScoring.Core.Export;

/// <summary>Rapport PDF détaillé Kumite (cahier 5.6.1) : par combat, historique horodaté, décision, résultat.</summary>
public class KumiteReportBuilder
{
    public byte[] Generer(Competition competition, List<Combat> combats, string? filtreCategorie = null)
    {
        var document = Document.Create(container =>
        {
            container.Page(page =>
            {
                page.Size(PageSizes.A4);
                page.Margin(30);
                page.DefaultTextStyle(x => x.FontSize(10));

                page.Header().Column(col =>
                {
                    col.Item().Text("Karate Scoring — Rapport Kumite").FontSize(18).Bold();
                    col.Item().Text($"{competition.Nom} — {competition.Date:dd/MM/yyyy}" +
                        (filtreCategorie != null ? $" — Catégorie : {filtreCategorie}" : " — Toutes catégories"))
                        .FontSize(10);
                });

                page.Content().PaddingTop(10).Column(col =>
                {
                    if (combats.Count == 0)
                    {
                        col.Item().Text("Aucun combat disputé.");
                        return;
                    }
                    foreach (var combat in combats.OrderBy(c => c.Tour).ThenBy(c => c.Id))
                        col.Item().PaddingBottom(10).Element(e => ComposeCombat(e, combat));
                });

                page.Footer().AlignCenter().Text(t =>
                {
                    t.CurrentPageNumber();
                    t.Span(" / ");
                    t.TotalPages();
                });
            });
        });

        return document.GeneratePdf();
    }

    private static void ComposeCombat(IContainer container, Combat combat)
    {
        container.Border(1).BorderColor(Colors.Grey.Lighten1).Padding(8).Column(col =>
        {
            var akaNom = combat.CompetiteurAka?.NomComplet ?? "-";
            var akaClub = combat.CompetiteurAka?.Club?.Nom ?? "-";
            var aoNom = combat.CompetiteurAo?.NomComplet ?? (combat.EstBye ? "(exempt)" : "-");
            var aoClub = combat.CompetiteurAo?.Club?.Nom ?? "-";

            col.Item().Text($"Combat #{combat.Id} — Tour {combat.Tour}" + (combat.EstBye ? " (exemption)" : "")).Bold();
            col.Item().Row(row =>
            {
                row.RelativeItem().Element(e => ComposeCompetiteur(e, combat.CompetiteurAka, "Aka", akaNom, akaClub));
                if (!combat.EstBye)
                    row.RelativeItem().Element(e => ComposeCompetiteur(e, combat.CompetiteurAo, "Ao", aoNom, aoClub));
            });

            if (!combat.EstBye)
            {
                col.Item().Text($"Score final : Aka {combat.ScoreAka} — Ao {combat.ScoreAo}");
                col.Item().Text($"Arbitre : {combat.ArbitreNom ?? "-"}    Durée : {(combat.DureeReelleSec != null ? combat.DureeReelleSec + " s" : "-")}    Décision : {LibelleDecision(combat.ModeDecision)}");
            }

            var vainqueurNom = combat.VainqueurCouleur == Couleur.Aka ? akaNom : combat.VainqueurCouleur == Couleur.Ao ? aoNom : "-";
            col.Item().Text($"Vainqueur : {vainqueurNom}").Bold();

            if (combat.Evenements.Count > 0)
            {
                col.Item().PaddingTop(4).Text("Historique :").Italic();
                foreach (var ev in combat.Evenements.OrderBy(e => e.TempsCombat))
                {
                    var desc = ev.Type == TypeEvenement.Point
                        ? $"{ev.TempsCombat:mm\\:ss} — Point ({ev.Points}) pour {ev.Couleur}"
                        : $"{ev.TempsCombat:mm\\:ss} — Pénalité {ev.Penalite} pour {ev.Couleur}";
                    col.Item().Text(desc).FontSize(9);
                }
            }
        });
    }

    private static void ComposeCompetiteur(IContainer container, Participant? participant, string couleur, string nom, string club)
    {
        container.PaddingTop(4).Row(row =>
        {
            row.ConstantItem(30).Height(30).Element(e =>
            {
                var photo = ChargerPhoto(participant);
                if (photo != null) e.Border(1).BorderColor(Colors.Grey.Lighten1).Image(photo).FitArea();
                else e.Border(1).BorderColor(Colors.Grey.Lighten2).Background(Colors.Grey.Lighten3);
            });
            row.RelativeItem().PaddingLeft(6).AlignMiddle().Text($"{couleur} : {nom} ({club})");
        });
    }

    /// <summary>Charge la photo importée d'un athlète pour l'incruster dans le PDF — même emplacement de
    /// stockage que l'API (uploads/, à côté de la base SQLite, jamais dans wwwroot). Absence de photo ou
    /// fichier introuvable : traité en silence (cadre gris neutre), pas une erreur de génération de rapport.</summary>
    private static byte[]? ChargerPhoto(Participant? participant)
    {
        if (participant?.PhotoExtension == null) return null;
        var uploadsDir = FkcScoringPaths.ResolveUploadsDir(FkcScoringPaths.ResolveDbPath());
        var chemin = ImageUploadService.CheminFichier(uploadsDir, "participant", participant.Id, participant.PhotoExtension);
        return File.Exists(chemin) ? File.ReadAllBytes(chemin) : null;
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
