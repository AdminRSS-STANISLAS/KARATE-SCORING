using FkcScoring.Core.Data.Entities;
using FkcScoring.Core.Domain;
using QuestPDF.Fluent;
using QuestPDF.Helpers;
using QuestPDF.Infrastructure;

namespace FkcScoring.Core.Export;

/// <summary>Rapport PDF détaillé Kata (cahier 5.6.2) : par confrontation, kata exécuté, votes de chaque juge, vainqueur.</summary>
public class KataReportBuilder
{
    public byte[] Generer(Competition competition, List<KataConfrontation> confrontations, IReadOnlyDictionary<int, Discipline> disciplineParTableau, string? filtreCategorie = null)
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
                    col.Item().Text("Karate Scoring — Rapport Kata").FontSize(18).Bold();
                    col.Item().Text($"{competition.Nom} — {competition.Date:dd/MM/yyyy}" +
                        (filtreCategorie != null ? $" — Catégorie : {filtreCategorie}" : " — Toutes catégories"))
                        .FontSize(10);
                });

                page.Content().PaddingTop(10).Column(col =>
                {
                    if (confrontations.Count == 0)
                    {
                        col.Item().Text("Aucune confrontation disputée.");
                        return;
                    }
                    foreach (var c in confrontations.OrderBy(c => c.Tour).ThenBy(c => c.Id))
                        col.Item().PaddingBottom(10).Element(e => ComposeConfrontation(e, c, disciplineParTableau[c.TableauId]));
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

    private static void ComposeConfrontation(IContainer container, KataConfrontation c, Discipline discipline)
    {
        container.Border(1).BorderColor(Colors.Grey.Lighten1).Padding(8).Column(col =>
        {
            var equipe = discipline == Discipline.KataEquipe;
            var aNom = equipe ? c.Equipe1?.Nom : c.Participant1?.NomComplet;
            var aClub = equipe ? c.Equipe1?.Club?.Nom : c.Participant1?.Club?.Nom;
            var bNom = equipe ? c.Equipe2?.Nom : c.Participant2?.NomComplet;
            var bClub = equipe ? c.Equipe2?.Club?.Nom : c.Participant2?.Club?.Nom;

            col.Item().Text($"Confrontation #{c.Id} — Tour {c.Tour}").Bold();
            col.Item().Text($"Aka : {aNom ?? "-"} ({aClub ?? "-"}) — kata {c.Kata1?.Nom ?? "-"}   vs   Ao : {bNom ?? "-"} ({bClub ?? "-"}) — kata {c.Kata2?.Nom ?? "-"}");

            if (c.Votes.Count > 0)
            {
                var votesAka = c.Votes.Count(v => v.VoteCouleur == Couleur.Aka);
                var votesAo = c.Votes.Count - votesAka;
                col.Item().Text($"Votes : {votesAka} Aka — {votesAo} Ao");
                col.Item().PaddingTop(2).Text("Détail des juges :").Italic();
                foreach (var vote in c.Votes.OrderBy(v => v.JugeNumero))
                    col.Item().Text($"Juge {vote.JugeNumero} : {vote.VoteCouleur}").FontSize(9);
            }

            var vainqueurNom = c.VainqueurCouleur == Couleur.Aka ? aNom : c.VainqueurCouleur == Couleur.Ao ? bNom : "-";
            col.Item().Text($"Vainqueur : {vainqueurNom}").Bold();
        });
    }
}
