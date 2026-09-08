# KARATE SCORING

Plateforme de gestion et d'arbitrage de compétitions de karaté (Kumite et Kata) — inscriptions, tirages, tableaux, chronométrage, scoring en direct, résultats et exports PDF/Excel.

Conçue pour fonctionner **sans connexion Internet**, sur un réseau local : un poste central (l'ordinateur de l'organisateur) sert l'application à un ou plusieurs postes de tatami (arbitrage, écran public) via Wi-Fi/Ethernet local.

## Démarrage rapide

```bash
dotnet run --project src/KarateScoring.Api
```

Puis ouvrir `http://localhost:5090` dans un navigateur. Voir [INSTALLATION.md](INSTALLATION.md) pour le détail (prérequis, base de données, réseau local) et [DEPLOYMENT.md](DEPLOYMENT.md) pour une installation en conditions de tournoi.

## Documentation

- [ARCHITECTURE.md](ARCHITECTURE.md) — structure du projet, stack technique, modèle de données
- [INSTALLATION.md](INSTALLATION.md) — installation et premier lancement
- [DEPLOYMENT.md](DEPLOYMENT.md) — mise en place le jour du tournoi (réseau local, multi-tatamis, sauvegarde)
- [USER_GUIDE.md](USER_GUIDE.md) — guide d'utilisation, écran par écran
- [API.md](API.md) — référence de l'API REST

## Fonctionnalités

- Gestion multi-tournois (compétitions, clubs, athlètes, catégories, inscriptions)
- Génération automatique de tableaux (poule, élimination directe, repêchage) selon l'effectif
- Arbitrage Kumite : chronomètre, points (Ippon/Waza-ari/Yuko), pénalités, Hantei, historique horodaté
- Jury Kata : confrontations directes Aka/Ao, vote des juges, kata paramétrable
- Multi-tatamis avec file d'attente (en cours / suivant / à venir) par aire de compétition
- Résultats automatiques, classements, exports PDF et Excel
- Sécurité : code administrateur pour les opérations sensibles, journal d'audit par opérateur
- Sauvegarde automatique périodique + restauration à chaud depuis l'interface, export vers clé USB

## Tests

```bash
dotnet test
```

## État du projet

Développé par phases (voir le prompt maître interne) : audit, architecture, scoring, tournois, multi-tatamis/réseau, UI/UX, sécurité, tests, sauvegarde — toutes terminées. Phase en cours : préparation commerciale (documentation, finitions).
