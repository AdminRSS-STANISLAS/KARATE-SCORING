# Brief technique — FKC Scoring

Application de gestion et d'arbitrage de compétitions de karaté Shotokan (Kumite et Kata), pour Fouda Karate Club (FKC).

## Contraintes clés
- Application **autonome**, sans serveur, sans synchronisation réseau.
- Un seul poste. Stockage local en **SQLite**.
- Cible : Windows desktop (setup installable) **et** Android (APK), depuis un seul code source.
- Stack recommandée : **Flutter** (Windows + Android depuis un seul projet), base **sqflite** ou **drift** pour SQLite.
- Règlement de référence : **WKF**, mais tous les barèmes doivent être paramétrables par compétition (voir ci-dessous), car ils évoluent (ex. la notation Kata a changé au 1er janvier 2026).

## Périmètre fonctionnel (V1)
1. Gestion des compétitions, catégories, clubs, participants, inscriptions.
2. Équipes (Kata par équipe).
3. Génération automatique du format de tableau selon l'effectif :
   - 2 participants → finale directe
   - 3–4 → poule unique
   - 5–7 → poule(s) puis élimination directe
   - 8+ → élimination directe + repêchage (deux médailles de bronze)
   - Seuils ajustables, format modifiable manuellement par l'organisateur.
4. Module Kumite : chronomètre, saisie Ippon (3) / Waza-ari (2) / Yuko (1), pénalités (Chukoku/Keikoku, Hansoku-chui/Hansoku), Kiken, Shikkaku, Hantei (tranché par drapeaux Aka/Ao), règle du Senshu, victoire à 8 points d'écart ou fin de temps. Chaque compétiteur d'un combat est identifié par une couleur (Aka = rouge, Ao = bleu). Durées et barème paramétrables par compétition.
5. Module Kata : structuré comme une **confrontation directe entre deux compétiteurs** (ou deux équipes), exactement comme le Kumite — pas une notation chiffrée individuelle. Chaque compétiteur d'une confrontation est identifié par une couleur (Aka = rouge, Ao = bleu). À chaque tour, les deux compétiteurs exécutent chacun leur kata, puis chaque juge lève le drapeau de la couleur (Aka ou Ao) du compétiteur qu'il juge vainqueur (nombre de juges paramétrable, typiquement 5 ou 7) ; le compétiteur totalisant le plus de drapeaux gagne la confrontation. Le tableau Kata suit les mêmes règles de format automatique que le Kumite (voir seuils ci-dessus). Le kata exécuté par chaque compétiteur est saisi **au moment de la confrontation**, par un juge principal unique, depuis une liste de katas personnalisable (pas figée dans le code).
6. Exports PDF : rapport détaillé Kumite (par combat : compétiteurs, club, arbitre, score, historique horodaté des points/pénalités, durée, mode de décision) et rapport détaillé Kata (par passage : compétiteur/équipe, kata exécuté, notes de chaque juge, score final, classement). Chaque rapport doit être disponible en version globale (toute la compétition) et filtrée par catégorie.
7. Journal d'audit des modifications de score.

Hors périmètre V1 (volontairement) : paiement en ligne, streaming, billetterie, app mobile séparée des arbitres/jurys en réseau, toute synchronisation multi-poste, tout serveur/base distante.

## Modèle de données (résumé)
Entités principales : `Competition`, `Categorie`, `Club`, `Participant`, `Inscription`, `Equipe`, `EquipeMembre`, `Tableau`, `Combat`, `EvenementCombat`, `Kata` (référentiel), `KataConfrontation`, `VoteJuge`, `Classement`.

`KataConfrontation` (remplace l'ancienne `PassageKata`) : id, tableau_id, tour, participant1_id (ou equipe1_id, couleur Aka), participant2_id (ou equipe2_id, couleur Ao), kata1_id, kata2_id, vainqueur_id. C'est le pendant Kata de `Combat` pour le Kumite.

`VoteJuge` (remplace l'ancienne `NoteJuge`) : id, confrontation_id, juge_numero, vote_couleur (Aka ou Ao).

`Combat` : ajouter un champ couleur par participant (Aka/Ao) — utilisé aussi pour le vote Hantei.

Champs de configuration réglementaire à porter sur `Competition` : durée par défaut du combat (par catégorie), nombre de juges Kata (5 ou 7 typiquement).

Détail des champs et relations pour la partie administrative (Competition, Categorie, Club, Participant, Inscription, Equipe) : voir le schéma ER produit dans la conversation — reste inchangé.

## Nom de l'application
**FKC Scoring** (nom retenu, logo du club fourni : cercle rouge/noir/blanc, silhouette de karatéka).

## Prochaines étapes suggérées pour Claude Code
1. Initialiser un projet Flutter (`flutter create fkc_scoring`).
2. Mettre en place le schéma SQLite (via `sqflite` ou `drift`) à partir du modèle de données ci-dessus.
3. Construire les écrans dans l'ordre de criticité : saisie Kumite (chrono + points) → saisie Kata (notes juge) → gestion compétitions/inscriptions → génération de tableaux → exports PDF.
4. Compiler : `flutter build windows` (+ empaquetage avec Inno Setup pour un setup.exe) et `flutter build apk` pour l'Android.
