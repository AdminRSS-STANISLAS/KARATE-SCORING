using FkcScoring.Core.Data;
using FkcScoring.Core.Domain;
using Microsoft.EntityFrameworkCore;

var builder = WebApplication.CreateBuilder(args);

builder.Services.AddControllers().AddJsonOptions(o =>
{
    o.JsonSerializerOptions.PropertyNamingPolicy = System.Text.Json.JsonNamingPolicy.CamelCase;
    o.JsonSerializerOptions.DefaultIgnoreCondition = System.Text.Json.Serialization.JsonIgnoreCondition.WhenWritingNull;
    o.JsonSerializerOptions.Converters.Add(new System.Text.Json.Serialization.JsonStringEnumConverter());
});
builder.Services.AddEndpointsApiExplorer();

var dbPath = Environment.GetEnvironmentVariable("KARATE_SCORING_DB_PATH") ?? "/data/karate-scoring.db";
Directory.CreateDirectory(Path.GetDirectoryName(dbPath)!);
builder.Services.AddDbContext<FkcScoringContext>(o => o.UseSqlite($"Data Source={dbPath}"));
builder.Services.AddScoped<AuditService>();

var app = builder.Build();

using (var scope = app.Services.CreateScope())
{
    var db = scope.ServiceProvider.GetRequiredService<FkcScoringContext>();
    db.Database.Migrate();
}

app.UseDefaultFiles();
app.UseStaticFiles();
app.MapControllers();
app.MapFallbackToFile("index.html");

app.Run();
