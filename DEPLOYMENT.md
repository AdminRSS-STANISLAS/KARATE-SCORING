# Déploiement — jour de tournoi

Ce guide couvre la mise en place réelle : un poste central qui sert plusieurs postes de tatami sur un réseau local, sans Internet.

## 1. Choisir le poste central

Le poste central héberge le serveur (`KarateScoring.Api`) et la base SQLite. Il doit rester allumé et connecté au réseau local pendant toute la durée du tournoi. Recommandé : un ordinateur portable branché sur secteur, avec un onduleur si possible.

## 2. Mettre en place le réseau local

- Un routeur/point d'accès Wi-Fi local suffit (pas besoin d'accès Internet — un routeur non connecté au WAN fonctionne).
- Connecter le poste central et tous les postes de tatami/écrans publics au même réseau (Wi-Fi ou Ethernet).
- **Pare-feu Windows** : au premier lancement, Windows peut demander d'autoriser `dotnet`/l'application à accepter les connexions entrantes sur le réseau — accepter pour les réseaux privés.

## 3. Démarrer le serveur

```bash
dotnet run --project src/KarateScoring.Api --configuration Release
```

Ou, après une publication (voir §6), lancer directement l'exécutable produit.

## 4. Trouver l'adresse à donner aux postes tatami

Dans l'application, l'écran **Sécurité** (ou l'appel `GET /api/network-info`) affiche les adresses IPv4 locales du poste central et le port d'écoute (par défaut `5090`). Donner l'URL correspondante (ex. `http://192.168.1.42:5090`) aux postes de tatami — ils l'ouvrent simplement dans leur navigateur.

Si plusieurs adresses IP apparaissent (ex. adaptateurs virtuels VPN/machine virtuelle), choisir celle du réseau Wi-Fi/Ethernet réellement utilisé pour le tournoi.

## 5. Configurer les tatamis

Sur l'écran **Tatamis**, créer une **Aire** par tapis de compétition, puis l'assigner à chaque **Tableau** concerné (écran Tableaux). Chaque poste de tatami ouvre l'écran **Arbitrage Kumite** ou **Jury Kata** filtré sur son aire, et suit la file d'attente (en cours / suivant / à venir).

## 6. Publier une version optimisée (optionnel, recommandé pour un vrai tournoi)

Sous Windows, le script fourni produit un exécutable autonome (aucun .NET SDK requis sur le poste organisateur) :

```powershell
./scripts/publish.ps1
```

Résultat dans `publish/win-x64/KarateScoring.Api.exe` (et une archive `publish/karate-scoring_win-x64.zip` prête à copier sur le poste organisateur). Pour une autre plateforme : `./scripts/publish.ps1 -Runtime linux-x64` ou `-Runtime osx-x64`.

Alternative manuelle (nécessite le .NET SDK sur le poste cible) :

```bash
dotnet publish src/KarateScoring.Api -c Release -o publish
cd publish
./KarateScoring.Api        # Linux/macOS
KarateScoring.Api.exe      # Windows
```

## 7. Sécuriser l'accès administrateur

Avant le tournoi, définir un code administrateur depuis l'écran **Sécurité** (`POST /api/securite/code`) — il protège la réinitialisation de la base et la restauration de sauvegarde. Sans code défini, ces opérations restent ouvertes à quiconque est sur le réseau local.

Chaque poste devrait aussi renseigner un nom d'opérateur (même écran) pour que le journal d'audit reste exploitable.

## 8. Sauvegarde pendant et après le tournoi

- Une sauvegarde automatique tourne en tâche de fond toutes les 15 minutes par défaut (`KARATE_SCORING_SAUVEGARDE_INTERVALLE_MIN`), conservant les 20 dernières.
- À tout moment, télécharger une sauvegarde à jour vers une clé USB depuis l'écran **Sauvegardes** (`GET /api/sauvegardes/telecharger`).
- En cas de panne du poste central, la base peut être restaurée sur un autre poste : copier le fichier `.db` sauvegardé à l'emplacement attendu (voir [INSTALLATION.md](INSTALLATION.md#emplacement-des-données)) ou l'importer via l'écran Sauvegardes.

## 9. Après le tournoi

Exporter les résultats (PDF/Excel) depuis l'écran **Résultats & exports**, et conserver une sauvegarde finale de la base (`GET /api/sauvegardes/telecharger`).

## Limites connues

- Pas de test de portée LAN automatisé — vérifier manuellement que chaque poste de tatami atteint bien le poste central avant le début des combats.
- Les adresses d'adaptateurs virtuels (VPN, machines virtuelles) peuvent apparaître dans la liste des IP locales à côté de la vraie IP réseau — l'organisateur doit choisir la bonne.
- Aucune synchronisation cloud à ce jour : le poste central est la seule source de vérité pendant le tournoi (architecture prévue pour une synchronisation future, voir [ARCHITECTURE.md](ARCHITECTURE.md)).
