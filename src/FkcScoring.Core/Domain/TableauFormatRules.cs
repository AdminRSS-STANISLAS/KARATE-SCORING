using FkcScoring.Core.Data.Entities;

namespace FkcScoring.Core.Domain;

/// <summary>Seuils déterminant le format de tableau selon l'effectif (ajustables par l'organisateur, cahier 5.3).</summary>
public class TableauFormatSeuils
{
    public int MaxPouleUnique { get; set; } = 4;
    public int MaxPoulePuisElimination { get; set; } = 7;

    public static TableauFormatSeuils Defaut => new();
}

public static class TableauFormatRules
{
    public static FormatTableau DeterminerFormat(int effectif, TableauFormatSeuils? seuils = null)
    {
        seuils ??= TableauFormatSeuils.Defaut;
        if (effectif < 2)
            throw new ArgumentException("Il faut au moins 2 compétiteurs pour générer un tableau.", nameof(effectif));

        if (effectif == 2) return FormatTableau.FinaleDirecte;
        if (effectif <= seuils.MaxPouleUnique) return FormatTableau.PouleUnique;
        if (effectif <= seuils.MaxPoulePuisElimination) return FormatTableau.PoulePuisElimination;
        return FormatTableau.EliminationRepechage;
    }
}
