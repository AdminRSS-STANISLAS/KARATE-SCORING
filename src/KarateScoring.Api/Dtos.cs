using FkcScoring.Core.Data.Entities;

namespace KarateScoring.Api;

public record CompetitionDto(int Id, string Nom, DateTime Date, string? Lieu, string Niveau,
    int PointsIppon, int PointsWazaAri, int PointsYuko, int EcartVictoire, int DureeCombatDefautSec,
    int NbJugesKataDefaut, int SeuilPouleUnique, int SeuilPoulePuisElimination);

public record CreateCompetitionRequest(string Nom, DateTime Date, string? Lieu, string Niveau);
public record ReglagesRequest(int DureeCombatDefautSec, int EcartVictoire, int NbJugesKataDefaut, int SeuilPouleUnique, int SeuilPoulePuisElimination);

public record KataDto(int Id, string Nom);
public record CreateKataRequest(string Nom);

public record CategorieDto(int Id, int CompetitionId, string Nom, string Discipline, string? Sexe,
    int? AgeMin, int? AgeMax, string? GradeMin, int InscritsCount, bool HasTableau);
public record CreateCategorieRequest(string Nom, string Discipline, string? Sexe, int? AgeMin, int? AgeMax, string? GradeMin);

public record ClubDto(int Id, string Nom, bool ALogo);
public record ResolveClubRequest(string Nom);

public record ParticipantDto(int Id, string Nom, string Prenom, string Club, string? Grade, DateTime? DateNaissance, string? Licence, double? Poids, bool APhoto);
public record CreateParticipantRequest(string Nom, string Prenom, string Club, string? Grade, DateTime? DateNaissance, string? Licence, double? Poids);
public record InscriptionsRequest(List<int> CategorieIds);

public record EquipeMembreDto(int Id, string Nom);
public record EquipeDto(int Id, string Nom, string Club, List<EquipeMembreDto> Membres, int? CategorieId);
public record CreateEquipeRequest(string Nom, string Club, List<int> MembreIds, int? CategorieId);

public record EvenementDto(string T, string Kind, string Couleur, string Label, int? Valeur);
public record VoteDto(int JugeNumero, string Couleur);

public record ConfrontationDto(
    int Id, int TableauId, int Tour, int? Moitie, string Type, bool EstBye, bool EstRepechage, string Statut,
    int? AId, string? ANom, string? AClub, int? BId, string? BNom, string? BClub,
    int? ScoreAka, int? ScoreAo, string? SenshuCouleur, string? ModeDecision, string? VainqueurCouleur, int? DureeReelleSec,
    List<EvenementDto>? Evenements,
    string? Kata1Nom, string? Kata2Nom, int? NbJuges, List<VoteDto>? Votes,
    int? ProchainCombatId, string? ProchainCombatCouleur,
    DateTime? ChronoDemarreLeUtc, int? ChronoRestantMs);

public record TableauDto(int Id, int CategorieId, string Format, List<ConfrontationDto> Confrontations, int? AireId, string? AireNom);

public record GenererTableauRequest(string? FormatForce);

public record AireDto(int Id, string Nom, int Ordre);
public record CreateAireRequest(string Nom);
public record RenameAireRequest(string Nom);
public record AssignerAireRequest(int? AireId);

public record FileAttenteEntryDto(string CategorieNom, ConfrontationDto Confrontation);
public record FileAttenteDto(string AireNom, FileAttenteEntryDto? EnCours, FileAttenteEntryDto? Suivant, List<FileAttenteEntryDto> AVenir);

public record NetworkInfoDto(int Port, List<string> Addresses);

public record PointRequest(string Couleur, string Type, int TempsEcouleSec);
public record PenaliteRequest(string Couleur, string Penalite, int TempsEcouleSec);
public record HanteiRequest(string Couleur);
public record FinDeTempsRequest(int TempsEcouleSec);
public record ChronoSyncRequest(bool Running, int RemainingMs);
public record DefinirKataRequest(string Couleur, int KataId);
public record VoteRequest(int JugeNumero, string Couleur);

public record ClassementDto(int Position, string? Medaille, string Nom, string Club);

public record AuditDto(DateTime Horodatage, string EntiteType, int EntiteId, string Action, string? AncienneValeur, string? NouvelleValeur, string? Utilisateur);

public record SecuriteStatusDto(bool CodeConfigure);
public record DefinirCodeRequest(string NouveauCode, string? AncienCode);
public record ResetRequest(string? Code);
public record RestaurerRequest(string? Code);
