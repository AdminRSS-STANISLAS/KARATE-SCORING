using FkcScoring.Core.Domain;

// Outil PRIVÉ : génère une clé d'activation Karate Scoring à partir du code que le client communique
// depuis son écran d'activation. À garder pour soi — quiconque a accès à cet outil (et au secret dans
// LicenceService.cs) peut générer des licences valides pour n'importe quel poste.
//
// Usage : dotnet run --project tools/LicenceKeygen -- "XXXX-XXXX-XXXX"

if (args.Length != 1 || string.IsNullOrWhiteSpace(args[0]))
{
    Console.WriteLine("Usage : dotnet run --project tools/LicenceKeygen -- \"<code du poste>\"");
    Console.WriteLine("Le code du poste est affiché sur l'écran d'activation du client (ex. F3A1-9B02-11CC).");
    return 1;
}

var empreinte = args[0];
var cle = LicenceService.GenererCle(empreinte);

Console.WriteLine($"Code du poste : {empreinte}");
Console.WriteLine($"Clé d'activation : {cle}");
return 0;
