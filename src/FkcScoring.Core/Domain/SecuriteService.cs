using System.Security.Cryptography;
using FkcScoring.Core.Data;
using Microsoft.EntityFrameworkCore;

namespace FkcScoring.Core.Domain;

/// <summary>
/// Protège les actions destructrices (reset complet, cahier 5.7/§21) par un code administrateur
/// unique, propre au poste — pas un vrai système de comptes/rôles, volontairement minimal pour ne
/// pas ajouter de friction pendant l'arbitrage.
/// </summary>
public class SecuriteService
{
    private const int Iterations = 100_000;
    private const int SaltSize = 16;
    private const int HashSize = 32;

    public static async Task<bool> EstConfigureAsync(FkcScoringContext db) =>
        (await Charger(db))?.AdminCodeHash != null;

    /// <summary>Définit un nouveau code. Exige l'ancien code si un code est déjà configuré (change de mot de passe), libre sinon (première configuration).</summary>
    public static async Task<bool> DefinirCodeAsync(FkcScoringContext db, string nouveauCode, string? ancienCode)
    {
        if (string.IsNullOrWhiteSpace(nouveauCode)) return false;
        var parametres = await Charger(db) ?? new Data.Entities.Parametres { Id = 1 };

        if (parametres.AdminCodeHash != null && !VerifierHash(parametres.AdminCodeHash, ancienCode ?? ""))
            return false;

        parametres.AdminCodeHash = CalculerHash(nouveauCode);
        if (db.Entry(parametres).State == EntityState.Detached) db.Add(parametres);
        await db.SaveChangesAsync();
        return true;
    }

    public static async Task<bool> VerifierCodeAsync(FkcScoringContext db, string? code)
    {
        var parametres = await Charger(db);
        if (parametres?.AdminCodeHash == null) return true; // pas encore configuré : pas de régression sur le comportement actuel
        return VerifierHash(parametres.AdminCodeHash, code ?? "");
    }

    private static Task<Data.Entities.Parametres?> Charger(FkcScoringContext db) =>
        db.Parametres.FirstOrDefaultAsync(p => p.Id == 1);

    private static string CalculerHash(string code)
    {
        var salt = RandomNumberGenerator.GetBytes(SaltSize);
        var hash = Rfc2898DeriveBytes.Pbkdf2(code, salt, Iterations, HashAlgorithmName.SHA256, HashSize);
        return $"{Iterations}.{Convert.ToBase64String(salt)}.{Convert.ToBase64String(hash)}";
    }

    private static bool VerifierHash(string encoded, string code)
    {
        var parts = encoded.Split('.');
        if (parts.Length != 3 || !int.TryParse(parts[0], out var iterations)) return false;
        var salt = Convert.FromBase64String(parts[1]);
        var attendu = Convert.FromBase64String(parts[2]);
        var calcule = Rfc2898DeriveBytes.Pbkdf2(code, salt, iterations, HashAlgorithmName.SHA256, attendu.Length);
        return CryptographicOperations.FixedTimeEquals(calcule, attendu);
    }
}
