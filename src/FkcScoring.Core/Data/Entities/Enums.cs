namespace FkcScoring.Core.Data.Entities;

public enum NiveauCompetition { Amicale, Club, Ligue, National }

public enum Discipline { KumiteIndividuel, KataIndividuel, KataEquipe }

public enum StatutInscription { Inscrit, Forfait, Remplace }

public enum FormatTableau { FinaleDirecte, PouleUnique, PoulePuisElimination, EliminationRepechage }

public enum Couleur { Aka, Ao }

public enum ModeDecision { EcartPoints, FinTemps, Hantei, Disqualification }

public enum StatutCombat { EnAttente, EnCours, Termine }

public enum TypeEvenement { Point, Penalite }

public enum TypePenalite { Chukoku, Keikoku, HansokuChui, Hansoku, Kiken, Shikkaku }

public enum Medaille { Or, Argent, Bronze1, Bronze2 }
