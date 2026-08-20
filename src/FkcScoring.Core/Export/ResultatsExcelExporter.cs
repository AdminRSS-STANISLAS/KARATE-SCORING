using ClosedXML.Excel;
using FkcScoring.Core.Data.Entities;

namespace FkcScoring.Core.Export;

/// <summary>Export Excel des résultats/podiums (cahier 5.6), pour archivage ou transmission fédérale.</summary>
public class ResultatsExcelExporter
{
    public byte[] Generer(Competition competition, List<(Categorie Categorie, List<Classement> Classement)> resultatsParCategorie)
    {
        using var workbook = new XLWorkbook();
        var sheet = workbook.Worksheets.Add("Résultats");

        sheet.Cell(1, 1).Value = "Compétition";
        sheet.Cell(1, 2).Value = competition.Nom;
        sheet.Cell(2, 1).Value = "Date";
        sheet.Cell(2, 2).Value = competition.Date.ToString("dd/MM/yyyy");

        var headerRow = 4;
        sheet.Cell(headerRow, 1).Value = "Catégorie";
        sheet.Cell(headerRow, 2).Value = "Position";
        sheet.Cell(headerRow, 3).Value = "Médaille";
        sheet.Cell(headerRow, 4).Value = "Compétiteur";
        sheet.Cell(headerRow, 5).Value = "Club";
        sheet.Range(headerRow, 1, headerRow, 5).Style.Font.SetBold();

        int row = headerRow + 1;
        foreach (var (categorie, classement) in resultatsParCategorie)
        {
            foreach (var c in classement.OrderBy(c => c.Position))
            {
                sheet.Cell(row, 1).Value = categorie.Nom;
                sheet.Cell(row, 2).Value = c.Position;
                sheet.Cell(row, 3).Value = c.Medaille?.ToString() ?? "";
                sheet.Cell(row, 4).Value = c.Participant?.NomComplet ?? c.Equipe?.Nom ?? "";
                sheet.Cell(row, 5).Value = c.Participant?.Club?.Nom ?? c.Equipe?.Club?.Nom ?? "";
                row++;
            }
        }

        sheet.Columns().AdjustToContents();

        using var stream = new MemoryStream();
        workbook.SaveAs(stream);
        return stream.ToArray();
    }
}
