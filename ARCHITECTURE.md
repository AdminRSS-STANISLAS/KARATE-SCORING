# Architecture

## Vue d'ensemble

KARATE SCORING est une application web **offline-first** : un serveur ASP.NET Core sert à la fois l'API REST et le frontend (fichiers statiques), et stocke tout dans une base **SQLite** locale sur le disque du poste central. Aucun service cloud n'est requis pour le fonctionnement d'un tournoi.

```text
                    PC CENTRAL (KarateScoring.Api)
                    ── ASP.NET Core / Kestrel ──
                    ── SQLite (fichier local)  ──
                              │
                        Réseau Wi-Fi/Ethernet local
                              │
          ┌───────────────────┼───────────────────┐
          ↓                   ↓                   ↓
      Navigateur          Navigateur          Navigateur
      Tatami 1            Tatami 2            Écran public
```

Chaque poste (organisateur, arbitre, écran public) est simplement un navigateur qui ouvre l'IP du poste central sur le réseau local — aucune installation cliente.

## Projets (solution `FkcScoring.sln`)

| Projet | Rôle |
|---|---|
| `src/KarateScoring.Api` | **Application active.** API REST ASP.NET Core (.NET 9) + frontend statique (`wwwroot/`, HTML/CSS/JS vanilla, sans framework front). C'est le produit livré. |
| `src/FkcScoring.Core` | Domaine métier partagé : entités EF Core, `DbContext`, moteurs de règles (scoring, tableaux, kata), services (sécurité, audit, sauvegarde, réseau, chemins). Référencé par l'API. |
| `src/FkcScoring.App` | **Legacy.** Prototype WPF desktop antérieur au pivot vers la plateforme web (un seul poste, sans réseau). Non maintenu depuis le pivot — conservé dans la solution mais hors du produit livré. |
| `tests/FkcScoring.Core.Tests` | Tests unitaires du domaine métier (scoring, tableaux, sécurité, sauvegarde, concurrence). |

## Frontend (`src/KarateScoring.Api/wwwroot/`)

Application monopage en JavaScript natif (pas de build step, pas de bundler) :

- `index.html` — coquille de page
- `app.js` (~1100 lignes) — routage par hash interne, rendu des écrans, appels `fetch` vers l'API
- `styles.css` — design system (variables CSS, thème sombre, polices Oswald/Public Sans), sidebar responsive (drawer sous 900px)

Écrans principaux : Compétitions, Catégories, Participants, Équipes Kata, Tableaux, Tatamis, Arbitrage Kumite, Jury Kata, Résultats & exports, Journal d'audit, Sécurité.

## Backend (`src/KarateScoring.Api/`)

- `Program.cs` — bootstrap Kestrel (écoute sur `0.0.0.0` pour être joignable depuis les autres postes du réseau local), migrations EF Core au démarrage, middleware de conflit de concurrence (409 sur `DbUpdateConcurrencyException`), service d'arrière-plan de sauvegarde automatique
- `Controllers/` — un contrôleur REST par domaine (voir [API.md](API.md))

## Domaine (`src/FkcScoring.Core/`)

- `Domain/` — entités EF Core (Competition, Categorie, Club, Participant, Inscription, Equipe, Tableau, Combat, Aire, KataConfrontation, VoteJuge, Parametres, …) et moteurs de règles (`CombatEngine`, `KataEngine`, `TableauService`, `KataTableauService`)
- `Data/` — `FkcScoringContext` (DbContext), `FkcScoringPaths` (résolution du chemin de la base), `NetworkConfig` (résolution de l'URL d'écoute et des IP locales)
- `Export/` — génération PDF/Excel des résultats, `SauvegardeService` (sauvegarde/restauration à chaud via `VACUUM INTO`)
- Services transverses : `SecuriteService` (code admin, hashage PBKDF2), `AuditService` (journal d'audit par opérateur, injecté via `IHttpContextAccessor`)

## Modèle de données (résumé)

Une **Competition** contient des **Categorie**, des **Club**, des **Participant** (via **Inscription**), des **Equipe** (Kata par équipe) et des **Aire** (tatamis). Chaque catégorie génère un **Tableau**, composé de **Combat** (Kumite) ou de **KataConfrontation** (Kata, avec **VoteJuge** par juge). Chaque combat/confrontation garde un historique horodaté de ses événements (points, pénalités, votes) et un jeton `RowVersion` pour la concurrence optimiste.

## Concurrence et fiabilité

- **Concurrence optimiste** : `Combat`/`KataConfrontation` portent un `RowVersion` EF Core — si deux postes modifient le même combat en même temps (ex. arbitre + poste de contrôle), le second `SaveChanges` échoue et l'API répond `409 Conflict` avec un message clair plutôt que d'écraser silencieusement les données.
- **Chronomètre** ancré sur l'horloge murale (pas des décréments à intervalle fixe) pour rester précis malgré les ralentissements navigateur, et persisté en `localStorage` pour survivre à un rechargement de page.
- **Sauvegarde** automatique périodique (`SauvegardeAutomatiqueHostedService`) via `VACUUM INTO`, transactionnellement cohérente même pendant une écriture concurrente ; restauration à chaud sans redémarrage du serveur.

## Sécurité

Modèle « protection minimale et pragmatique » (choix produit assumé, pas un système de rôles complet) : un code administrateur (PBKDF2) protège les opérations sensibles (réinitialisation de la base, restauration de sauvegarde) ; chaque poste s'identifie par un nom d'opérateur (en-tête `X-Operateur`) tracé dans le journal d'audit. Pas d'authentification par compte/session — le réseau local du tournoi est considéré comme le périmètre de confiance.

## Voir aussi

- [API.md](API.md) pour la liste complète des routes REST
- [DEPLOYMENT.md](DEPLOYMENT.md) pour la configuration réseau en conditions réelles
