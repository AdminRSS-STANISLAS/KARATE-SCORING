using FkcScoring.Core.Data.Entities;
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
            col.Item().Text($"Aka : {akaNom} ({akaClub})   vs   Ao : {aoNom} ({aoClub})");

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

    private static string LibelleDecision(ModeDecision? mode) => mode switch
    {
        ModeDecision.EcartPoints => "écart de points",
        ModeDecision.FinTemps => "fin de temps réglementaire",
        ModeDecision.Hantei => "décision arbitrale (Hantei)",
        ModeDecision.Disqualification => "disqualification",
        _ => "-"
    };
}
