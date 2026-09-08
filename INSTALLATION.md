# Installation

## Prérequis

- [.NET SDK 9.0](https://dotnet.microsoft.com/download/dotnet/9.0) ou supérieur
- Windows, macOS ou Linux (le projet cible `net9.0`, multiplateforme)
- Aucune base de données externe à installer : SQLite est embarqué

Vérifier l'installation :

```bash
dotnet --version
```

## Récupérer le projet

```bash
git clone <url-du-dépôt>
cd "KARATE SCORING"
```

## Premier lancement (poste unique, développement)

```bash
dotnet run --project src/KarateScoring.Api
```

Au premier démarrage :
1. Le dossier de données est créé automatiquement (`%LOCALAPPDATA%\KarateScoring` sous Windows, équivalent standard sous macOS/Linux).
2. La base SQLite est créée et les migrations EF Core sont appliquées automatiquement (`Database.Migrate()` dans `Program.cs`) — aucune commande manuelle requise.
3. Le serveur écoute par défaut sur toutes les interfaces réseau, port **5090** (`http://0.0.0.0:5090`).

Ouvrir `http://localhost:5090` dans un navigateur.

## Emplacement des données

| Donnée | Emplacement par défaut | Variable de surcharge |
|---|---|---|
| Base SQLite | `%LOCALAPPDATA%\KarateScoring\karate-scoring.db` | `KARATE_SCORING_DB_PATH` |
| Sauvegardes | `%LOCALAPPDATA%\KarateScoring\backups\` | (dérivé du chemin de la base) |

## Variables d'environnement

| Variable | Effet | Défaut |
|---|---|---|
| `KARATE_SCORING_DB_PATH` | Chemin complet du fichier `.db` SQLite | dossier utilisateur standard |
| `KARATE_SCORING_URLS` | URL(s) d'écoute Kestrel (équivalent `ASPNETCORE_URLS`, prioritaire si `ASPNETCORE_URLS` n'est pas défini) | `http://0.0.0.0:5090` |
| `KARATE_SCORING_SAUVEGARDE_INTERVALLE_MIN` | Intervalle (minutes) entre deux sauvegardes automatiques | `15` |
| `ASPNETCORE_URLS` | Standard ASP.NET Core, prévaut sur `KARATE_SCORING_URLS` si défini | — |

Exemple pour changer le port :

```bash
KARATE_SCORING_URLS="http://0.0.0.0:8080" dotnet run --project src/KarateScoring.Api
```

## Exécuter les tests

```bash
dotnet test
```

## Vérifier que tout fonctionne

1. Ouvrir `http://localhost:5090`
2. Créer une compétition (écran **Compétitions**)
3. Ajouter une catégorie et quelques participants
4. Générer un tableau et lancer un combat de test

Pour une installation en conditions réelles de tournoi (plusieurs postes de tatami sur le réseau local), voir [DEPLOYMENT.md](DEPLOYMENT.md).
