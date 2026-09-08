# API REST

Toutes les routes sont préfixées par `/api`, servies par la même instance Kestrel que le frontend. Format des échanges : JSON (`camelCase`), sauf indication contraire. Pas d'authentification par session — voir [ARCHITECTURE.md](ARCHITECTURE.md#sécurité) pour le modèle de sécurité (code administrateur sur les opérations sensibles).

En-tête optionnel `X-Operateur` : nom de l'opérateur du poste, injecté automatiquement dans le journal d'audit pour chaque requête qui modifie des données.

Réponses d'erreur communes :
- `400 Bad Request` — requête invalide (validation métier)
- `403 Forbidden` — code administrateur manquant ou incorrect
- `404 Not Found` — ressource introuvable
- `409 Conflict` — la ressource a été modifiée entre-temps par un autre poste (concurrence optimiste) : `{ "detail": "Ces données ont été modifiées par un autre poste entre-temps. Rechargez et réessayez." }`

## Compétitions — `/api/competitions`

| Méthode | Route | Description |
|---|---|---|
| GET | `/api/competitions` | Lister toutes les compétitions |
| GET | `/api/competitions/{id}` | Détail d'une compétition |
| POST | `/api/competitions` | Créer une compétition |
| PUT | `/api/competitions/{id}/reglages` | Modifier les réglages (durées, barème, seuils de format, nb de juges Kata par défaut) |

## Catégories — `/api`

| Méthode | Route | Description |
|---|---|---|
| GET | `/api/competitions/{competitionId}/categories` | Catégories d'une compétition |
| POST | `/api/competitions/{competitionId}/categories` | Créer une catégorie |
| DELETE | `/api/categories/{id}` | Supprimer une catégorie |

## Participants — `/api/participants`

| Méthode | Route | Description |
|---|---|---|
| GET | `/api/participants` | Lister les athlètes |
| POST | `/api/participants` | Créer un athlète |
| DELETE | `/api/participants/{id}` | Supprimer un athlète |
| GET | `/api/participants/{id}/inscriptions` | Inscriptions d'un athlète |
| PUT | `/api/participants/{id}/inscriptions` | Modifier les inscriptions d'un athlète |

## Clubs — `/api/clubs`

| Méthode | Route | Description |
|---|---|---|
| GET | `/api/clubs` | Lister les clubs |
| POST | `/api/clubs/resolve` | Résoudre/créer un club par nom (rapprochement accent-insensible pour éviter les doublons) |

## Équipes (Kata par équipe) — `/api`

| Méthode | Route | Description |
|---|---|---|
| GET | `/api/competitions/{competitionId}/equipes` | Lister les équipes |
| POST | `/api/competitions/{competitionId}/equipes` | Créer une équipe |
| DELETE | `/api/equipes/{id}` | Supprimer une équipe |

## Katas (référentiel) — `/api/katas`

| Méthode | Route | Description |
|---|---|---|
| GET | `/api/katas` | Lister les katas du référentiel |
| POST | `/api/katas` | Ajouter un kata |
| DELETE | `/api/katas/{id}` | Supprimer un kata |

## Tableaux — `/api`

| Méthode | Route | Description |
|---|---|---|
| GET | `/api/categories/{categorieId}/tableau` | Tableau d'une catégorie (`null` si non généré) |
| POST | `/api/categories/{categorieId}/tableau/generer` | Générer le tableau (`{ formatForce?: "PouleUnique"\|"PoulePuisElimination"\|"EliminationRepechage" }` pour forcer un format) |
| DELETE | `/api/tableaux/{tableauId}` | Supprimer le tableau (pour régénération) |
| POST | `/api/tableaux/{tableauId}/phase-elimination` | Générer la phase à élimination directe après les poules |
| POST | `/api/tableaux/{tableauId}/aire` | Assigner une aire (tatami) au tableau — `{ aireId: number\|null }` |

## Aires (tatamis) — `/api`

| Méthode | Route | Description |
|---|---|---|
| GET | `/api/competitions/{competitionId}/aires` | Lister les aires |
| POST | `/api/competitions/{competitionId}/aires` | Créer une aire |
| PUT | `/api/aires/{id}` | Modifier une aire |
| DELETE | `/api/aires/{id}` | Supprimer une aire |
| GET | `/api/aires/{id}/file-attente` | File d'attente de l'aire : en cours / suivant / à venir |

## Combats (Kumite) — `/api/combats`

| Méthode | Route | Description |
|---|---|---|
| GET | `/api/combats/{id}` | Détail d'un combat |
| POST | `/api/combats/{id}/demarrer` | Démarrer le combat |
| POST | `/api/combats/{id}/point` | Ajouter un point — `{ couleur: "Aka"\|"Ao", type: "ippon"\|"wazaari"\|"yuko", tempsEcouleSec }` |
| POST | `/api/combats/{id}/penalite` | Appliquer une pénalité — `{ couleur, penalite: "Chukoku"\|"Keikoku"\|"HansokuChui"\|"Hansoku"\|"Kiken"\|"Shikkaku", tempsEcouleSec }` |
| POST | `/api/combats/{id}/fin-de-temps` | Signaler la fin du temps réglementaire — `{ tempsEcouleSec? }` |
| POST | `/api/combats/{id}/hantei` | Enregistrer la décision arbitrale en cas d'égalité — `{ couleur: "Aka"\|"Ao" }` |

Le combat se termine et le résultat est enregistré automatiquement (mise à jour du tableau, combat suivant déterminé) dès qu'un écart de points suffisant, une disqualification, une fin de temps décisive ou un Hantei y met fin.

## Confrontations Kata — `/api/kata-confrontations`

| Méthode | Route | Description |
|---|---|---|
| GET | `/api/kata-confrontations/{id}` | Détail d'une confrontation |
| POST | `/api/kata-confrontations/{id}/kata` | Définir le kata exécuté par un compétiteur — `{ couleur, kataId }` |
| POST | `/api/kata-confrontations/{id}/vote` | Enregistrer le vote d'un juge — `{ jugeNumero, couleur }` |
| POST | `/api/kata-confrontations/{id}/valider` | Trancher la confrontation à partir des votes |

## Résultats & exports — `/api`

| Méthode | Route | Description |
|---|---|---|
| GET | `/api/categories/{categorieId}/classement` | Classement calculé d'une catégorie |
| GET | `/api/competitions/{competitionId}/export/kumite?categorie=` | Rapport PDF Kumite (global ou filtré par catégorie) |
| GET | `/api/competitions/{competitionId}/export/kata?categorie=` | Rapport PDF Kata (global ou filtré par catégorie) |
| GET | `/api/competitions/{competitionId}/export/excel` | Export Excel des résultats de la compétition |

## Sécurité — `/api/securite`

| Méthode | Route | Description |
|---|---|---|
| GET | `/api/securite` | Statut : `{ estConfigure: bool }` — un code administrateur est-il défini |
| POST | `/api/securite/code` | Définir/changer le code — `{ nouveauCode, ancienCode? }` (`ancienCode` requis si un code existe déjà, min. 4 caractères) |

## Sauvegardes — `/api/sauvegardes`

| Méthode | Route | Description |
|---|---|---|
| GET | `/api/sauvegardes` | Lister les sauvegardes disponibles |
| GET | `/api/sauvegardes/telecharger` | Télécharger une sauvegarde à jour (`.db`, cohérente même en usage actif) |
| POST | `/api/sauvegardes/{nom}/restaurer` | Restaurer une sauvegarde existante — `{ code }` (code administrateur requis) |
| POST | `/api/sauvegardes/importer` | Importer et restaurer un fichier `.db` — `multipart/form-data` (`fichier`, `code`) |

Chaque restauration/import prend automatiquement une sauvegarde de sécurité (`avant_restauration`) avant d'écraser la base.

## Administration — `/api/admin`

| Méthode | Route | Description |
|---|---|---|
| POST | `/api/admin/reset` | Vider toutes les données de la plateforme — `{ code? }` (code administrateur requis une fois configuré). Sauvegarde automatique avant réinitialisation ; en cas d'échec de la sauvegarde, la base n'est pas touchée. |

## Réseau — `/api/network-info`

| Méthode | Route | Description |
|---|---|---|
| GET | `/api/network-info` | Port d'écoute et adresses IPv4 locales du poste central, à donner aux postes de tatami |

## Journal d'audit — `/api/audit`

| Méthode | Route | Description |
|---|---|---|
| GET | `/api/audit` | Historique des opérations (opérateur, date/heure, action, entité, ancienne/nouvelle valeur) |
