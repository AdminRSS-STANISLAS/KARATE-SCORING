using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Design;

namespace FkcScoring.Core.Data;

/// <summary>Fabrique utilisée uniquement par les outils EF Core (dotnet-ef) pour générer les migrations.</summary>
public class FkcScoringContextFactory : IDesignTimeDbContextFactory<FkcScoringContext>
{
    public FkcScoringContext CreateDbContext(string[] args)
    {
        var options = new DbContextOptionsBuilder<FkcScoringContext>()
            .UseSqlite("Data Source=design_time.db")
            .Options;
        return new FkcScoringContext(options);
    }
}
