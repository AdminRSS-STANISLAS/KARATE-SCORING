using FkcScoring.Core.Data.Entities;
using FkcScoring.Core.Domain;

namespace KarateScoring.Api;

public static class Mapping
{
    public static CompetitionDto ToDto(this Competition c) => new(
        c.Id, c.Nom, c.Date, c.Lieu, c.Niveau.ToString(),
        c.PointsIppon, c.PointsWazaAri, c.PointsYuko, c.EcartVictoire, c.DureeCombatDefautSec,
        c.NbJugesKataDefaut, c.SeuilPouleUnique, c.SeuilPoulePuisElimination);

    public static KataDto ToDto(this Kata k) => new(k.Id, k.Nom);

    public static ClubDto ToDto(this Club c) => new(c.Id, c.Nom, c.LogoExtension != null);

    public static ParticipantDto ToDto(this Participant p) => new(
        p.Id, p.Nom, p.Prenom, p.Club?.Nom ?? "", p.Grade, p.DateNaissance, p.NumeroLicence, p.PoidsKg, p.PhotoExtension != null);

    public static EquipeDto ToDto(this Equipe e, int? categorieId) => new(
        e.Id, e.Nom, e.Club?.Nom ?? "",
        e.Membres.Select(m => new EquipeMembreDto(m.ParticipantId, m.Participant?.NomComplet ?? "")).ToList(),
        categorieId);

    public static ConfrontationDto ToDto(this Combat c) => new(
        c.Id, c.TableauId, c.Tour, c.Moitie, "kumite", c.EstBye, c.EstRepechage, c.Statut.ToString(),
        c.CompetiteurAkaId, c.CompetiteurAka?.NomComplet, c.CompetiteurAka?.Club?.Nom,
        c.CompetiteurAoId, c.CompetiteurAo?.NomComplet, c.CompetiteurAo?.Club?.Nom,
        c.ScoreAka, c.ScoreAo, c.SenshuCouleur?.ToString(), c.ModeDecision?.ToString(), c.VainqueurCouleur?.ToString(), c.DureeReelleSec,
        c.Evenements.OrderByDescending(e => e.TempsCombat).Select(e => new EvenementDto(
            $"{(int)e.TempsCombat.TotalMinutes:00}:{e.TempsCombat.Seconds:00}",
            e.Type == TypeEvenement.Point ? "point" : "penalite",
            e.Couleur.ToString(),
            e.Type == TypeEvenement.Point ? PointLabel(e.Points) : e.Penalite.ToString() ?? "",
            e.Points)).ToList(),
        null, null, null, null,
        c.ProchainCombatId, c.ProchainCombatCouleur?.ToString(),
        c.ChronoDemarreLeUtc, c.ChronoRestantMs);

    private static string PointLabel(int? points) => points switch { 3 => "Ippon", 2 => "Waza-ari", 1 => "Yuko", _ => "Point" };

    public static ConfrontationDto ToDto(this KataConfrontation c, Discipline discipline)
    {
        var aId = KataTableauService.CompetiteurId(c, Couleur.Aka, discipline);
        var bId = KataTableauService.CompetiteurId(c, Couleur.Ao, discipline);
        var equipe = discipline == Discipline.KataEquipe;
        var aNom = equipe ? c.Equipe1?.Nom : c.Participant1?.NomComplet;
        var aClub = equipe ? c.Equipe1?.Club?.Nom : c.Participant1?.Club?.Nom;
        var bNom = equipe ? c.Equipe2?.Nom : c.Participant2?.NomComplet;
        var bClub = equipe ? c.Equipe2?.Club?.Nom : c.Participant2?.Club?.Nom;

        return new ConfrontationDto(
            c.Id, c.TableauId, c.Tour, c.Moitie, "kata", c.EstBye, c.EstRepechage, c.Statut.ToString(),
            aId, aNom, aClub, bId, bNom, bClub,
            null, null, null, null, c.VainqueurCouleur?.ToString(), null,
            null,
            c.Kata1?.Nom, c.Kata2?.Nom, c.NbJuges,
            c.Votes.Select(v => new VoteDto(v.JugeNumero, v.VoteCouleur.ToString())).ToList(),
            c.ProchainConfrontationId, c.ProchainConfrontationCouleur?.ToString(),
            null, null);
    }

    public static AireDto ToDto(this Aire a) => new(a.Id, a.Nom, a.Ordre);

    public static ClassementDto ToDto(this Classement c) => new(
        c.Position, c.Medaille?.ToString(),
        c.Participant?.NomComplet ?? c.Equipe?.Nom ?? "—",
        c.Participant?.Club?.Nom ?? c.Equipe?.Club?.Nom ?? "—");
}
