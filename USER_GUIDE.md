# Guide d'utilisation

Ce guide décrit l'application écran par écran, dans l'ordre d'un tournoi type. Voir [DEPLOYMENT.md](DEPLOYMENT.md) pour la mise en réseau des postes.

## Compétitions

Créer une compétition (nom, date, lieu, organisateur, règlement) et l'activer. C'est le point d'entrée : les autres écrans travaillent sur la compétition active. Plusieurs compétitions peuvent coexister, chacune avec son propre historique. Les katas du référentiel (liste personnalisable utilisée en Jury Kata) se gèrent aussi ici.

## Catégories

Pour la compétition active, créer les catégories (âge, sexe, poids, discipline Kumite/Kata, niveau). Chaque athlète inscrit y sera rattaché — l'application empêche une affectation incohérente.

## Participants

Ajouter les athlètes (nom, prénom, sexe, date de naissance, club, numéro, statut) et gérer leurs inscriptions par catégorie. Recherche et filtrage disponibles pour éviter les doublons de club (résolution accent-insensible).

## Équipes Kata

Constituer les équipes pour les épreuves de Kata par équipe (regroupement de participants déjà inscrits).

## Tableaux

Pour chaque catégorie, générer le tableau automatiquement : le format (poule, élimination directe, avec ou sans repêchage) est choisi selon l'effectif, avec des seuils ajustables. Le tirage évite autant que possible les rencontres de premier tour entre athlètes du même club. Le tableau reste visualisable à tout moment (poule → quart → demi → finale). C'est aussi ici qu'on assigne une **aire** (tatami) à un tableau.

## Tatamis

Créer les aires de compétition (Tatami 1, Tatami 2, …) et suivre, pour chacune, la file d'attente : combat **en cours**, combat **suivant**, combats **à venir**. Permet à un opérateur de tatami de savoir instantanément quoi faire sans chercher manuellement dans les tableaux.

## Arbitrage Kumite

Écran opérateur pour un combat en cours :
- **Score** : Ippon (3), Waza-ari (2), Yuko (1) pour chaque compétiteur (Aka / Ao)
- **Pénalités** : Chukoku, Keikoku, Hansoku-chui, Hansoku (ces deux dernières disqualifiantes), Kiken, Shikkaku
- **Chronomètre** : démarrage, pause, reprise — ancré sur l'horloge murale pour rester précis même en cas de ralentissement
- **Senshu** : marqué automatiquement pour le premier à marquer en phase de poule
- **Fin de combat** : automatique à l'écart de points réglementaire ou à la fin du temps ; en cas d'égalité, bascule sur **Hantei** (décision arbitrale par drapeaux)
- Toutes les actions sont horodatées dans l'historique du combat et consignées dans le journal d'audit

Le résultat, une fois le combat terminé, met à jour automatiquement le tableau et détermine le combat suivant — aucune saisie manuelle supplémentaire n'est nécessaire.

## Jury Kata

Une confrontation Kata oppose deux compétiteurs (ou équipes), Aka contre Ao, comme au Kumite. Pour chaque tour : saisir le kata exécuté par chaque compétiteur, recueillir le vote de chaque juge (drapeau Aka ou Ao), puis valider — le vainqueur est celui qui totalise le plus de drapeaux.

## Résultats & exports

Classements par catégorie, calculés automatiquement à partir des tableaux terminés. Exports :
- **PDF Kumite** — détail par combat (compétiteurs, club, arbitre, score, historique horodaté, durée, mode de décision), global ou filtré par catégorie
- **PDF Kata** — détail par confrontation (compétiteurs/équipes, katas exécutés, votes, classement)
- **Excel** — export tabulaire des résultats

## Journal d'audit

Historique de toutes les opérations sensibles (qui, quand, quoi, sur quel combat/tatami/appareil), utile pour retracer une contestation après-coup.

## Sécurité

- Définir/changer le **code administrateur** (protège la réinitialisation de la base et la restauration de sauvegarde)
- Renseigner le **nom d'opérateur** du poste (apparaît dans le journal d'audit)
- Consulter l'**adresse réseau locale** du poste central à donner aux postes tatami (voir [DEPLOYMENT.md](DEPLOYMENT.md))
- Gérer les **sauvegardes** : liste, téléchargement (clé USB), restauration à chaud, import d'un fichier `.db`

## Cas d'usage complet

```text
Créer tournoi → Ajouter clubs → Ajouter/importer athlètes → Créer catégories
→ Générer tableaux → Attribuer tatamis → Lancer combat → Scorer AKA/AO
→ Chronomètre → Terminer combat → Résultat automatique → Combat suivant
→ Finale → Classement → Résultats → PDF
```

Ce scénario complet fonctionne intégralement hors ligne, sur le réseau local du tournoi.
