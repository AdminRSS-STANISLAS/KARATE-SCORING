using FkcScoring.Core.Data.Entities;
using FkcScoring.Core.Domain;
using Xunit;

namespace FkcScoring.Core.Tests;

public class TableauFormatRulesTests
{
    [Theory]
    [InlineData(2, FormatTableau.FinaleDirecte)]
    [InlineData(3, FormatTableau.PouleUnique)]
    [InlineData(4, FormatTableau.PouleUnique)]
    [InlineData(5, FormatTableau.PoulePuisElimination)]
    [InlineData(7, FormatTableau.PoulePuisElimination)]
    [InlineData(8, FormatTableau.EliminationRepechage)]
    [InlineData(16, FormatTableau.EliminationRepechage)]
    public void DeterminerFormat_RespecteLesSeuilsDuCahierDesCharges(int effectif, FormatTableau attendu)
    {
        Assert.Equal(attendu, TableauFormatRules.DeterminerFormat(effectif));
    }

    [Fact]
    public void DeterminerFormat_MoinsDeDeux_Leve()
    {
        Assert.Throws<ArgumentException>(() => TableauFormatRules.DeterminerFormat(1));
    }

    [Fact]
    public void DeterminerFormat_SeuilsPersonnalises_SontRespectes()
    {
        var seuils = new TableauFormatSeuils { MaxPouleUnique = 3, MaxPoulePuisElimination = 6 };
        Assert.Equal(FormatTableau.PoulePuisElimination, TableauFormatRules.DeterminerFormat(4, seuils));
        Assert.Equal(FormatTableau.EliminationRepechage, TableauFormatRules.DeterminerFormat(7, seuils));
    }
}
