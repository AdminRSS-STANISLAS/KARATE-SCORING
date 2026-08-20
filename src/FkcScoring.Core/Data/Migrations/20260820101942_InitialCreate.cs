using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

#pragma warning disable CA1814 // Prefer jagged arrays over multidimensional

namespace FkcScoring.Core.Data.Migrations
{
    /// <inheritdoc />
    public partial class InitialCreate : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "AuditLogs",
                columns: table => new
                {
                    Id = table.Column<int>(type: "INTEGER", nullable: false)
                        .Annotation("Sqlite:Autoincrement", true),
                    EntiteType = table.Column<string>(type: "TEXT", nullable: false),
                    EntiteId = table.Column<int>(type: "INTEGER", nullable: false),
                    Action = table.Column<string>(type: "TEXT", nullable: false),
                    AncienneValeur = table.Column<string>(type: "TEXT", nullable: true),
                    NouvelleValeur = table.Column<string>(type: "TEXT", nullable: true),
                    Utilisateur = table.Column<string>(type: "TEXT", nullable: true),
                    Horodatage = table.Column<DateTime>(type: "TEXT", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_AuditLogs", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "Clubs",
                columns: table => new
                {
                    Id = table.Column<int>(type: "INTEGER", nullable: false)
                        .Annotation("Sqlite:Autoincrement", true),
                    Nom = table.Column<string>(type: "TEXT", nullable: false),
                    Ville = table.Column<string>(type: "TEXT", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Clubs", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "Competitions",
                columns: table => new
                {
                    Id = table.Column<int>(type: "INTEGER", nullable: false)
                        .Annotation("Sqlite:Autoincrement", true),
                    Nom = table.Column<string>(type: "TEXT", nullable: false),
                    Date = table.Column<DateTime>(type: "TEXT", nullable: false),
                    Lieu = table.Column<string>(type: "TEXT", nullable: true),
                    Niveau = table.Column<int>(type: "INTEGER", nullable: false),
                    Reglement = table.Column<string>(type: "TEXT", nullable: false),
                    PointsIppon = table.Column<int>(type: "INTEGER", nullable: false),
                    PointsWazaAri = table.Column<int>(type: "INTEGER", nullable: false),
                    PointsYuko = table.Column<int>(type: "INTEGER", nullable: false),
                    EcartVictoire = table.Column<int>(type: "INTEGER", nullable: false),
                    DureeCombatDefautSec = table.Column<int>(type: "INTEGER", nullable: false),
                    NbJugesKataDefaut = table.Column<int>(type: "INTEGER", nullable: false),
                    CreatedAt = table.Column<DateTime>(type: "TEXT", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Competitions", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "Katas",
                columns: table => new
                {
                    Id = table.Column<int>(type: "INTEGER", nullable: false)
                        .Annotation("Sqlite:Autoincrement", true),
                    Nom = table.Column<string>(type: "TEXT", nullable: false),
                    Actif = table.Column<bool>(type: "INTEGER", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Katas", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "Participants",
                columns: table => new
                {
                    Id = table.Column<int>(type: "INTEGER", nullable: false)
                        .Annotation("Sqlite:Autoincrement", true),
                    Nom = table.Column<string>(type: "TEXT", nullable: false),
                    Prenom = table.Column<string>(type: "TEXT", nullable: false),
                    ClubId = table.Column<int>(type: "INTEGER", nullable: false),
                    Grade = table.Column<string>(type: "TEXT", nullable: true),
                    DateNaissance = table.Column<DateTime>(type: "TEXT", nullable: true),
                    NumeroLicence = table.Column<string>(type: "TEXT", nullable: true),
                    Sexe = table.Column<string>(type: "TEXT", nullable: true),
                    PoidsKg = table.Column<double>(type: "REAL", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Participants", x => x.Id);
                    table.ForeignKey(
                        name: "FK_Participants_Clubs_ClubId",
                        column: x => x.ClubId,
                        principalTable: "Clubs",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "Categories",
                columns: table => new
                {
                    Id = table.Column<int>(type: "INTEGER", nullable: false)
                        .Annotation("Sqlite:Autoincrement", true),
                    CompetitionId = table.Column<int>(type: "INTEGER", nullable: false),
                    Nom = table.Column<string>(type: "TEXT", nullable: false),
                    Discipline = table.Column<int>(type: "INTEGER", nullable: false),
                    AgeMin = table.Column<int>(type: "INTEGER", nullable: true),
                    AgeMax = table.Column<int>(type: "INTEGER", nullable: true),
                    Sexe = table.Column<string>(type: "TEXT", nullable: true),
                    GradeMin = table.Column<string>(type: "TEXT", nullable: true),
                    GradeMax = table.Column<string>(type: "TEXT", nullable: true),
                    PoidsMin = table.Column<double>(type: "REAL", nullable: true),
                    PoidsMax = table.Column<double>(type: "REAL", nullable: true),
                    DureeCombatSec = table.Column<int>(type: "INTEGER", nullable: true),
                    NbJugesKata = table.Column<int>(type: "INTEGER", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Categories", x => x.Id);
                    table.ForeignKey(
                        name: "FK_Categories_Competitions_CompetitionId",
                        column: x => x.CompetitionId,
                        principalTable: "Competitions",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "Equipes",
                columns: table => new
                {
                    Id = table.Column<int>(type: "INTEGER", nullable: false)
                        .Annotation("Sqlite:Autoincrement", true),
                    CategorieId = table.Column<int>(type: "INTEGER", nullable: false),
                    Nom = table.Column<string>(type: "TEXT", nullable: false),
                    ClubId = table.Column<int>(type: "INTEGER", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Equipes", x => x.Id);
                    table.ForeignKey(
                        name: "FK_Equipes_Categories_CategorieId",
                        column: x => x.CategorieId,
                        principalTable: "Categories",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_Equipes_Clubs_ClubId",
                        column: x => x.ClubId,
                        principalTable: "Clubs",
                        principalColumn: "Id");
                });

            migrationBuilder.CreateTable(
                name: "Tableaux",
                columns: table => new
                {
                    Id = table.Column<int>(type: "INTEGER", nullable: false)
                        .Annotation("Sqlite:Autoincrement", true),
                    CategorieId = table.Column<int>(type: "INTEGER", nullable: false),
                    Format = table.Column<int>(type: "INTEGER", nullable: false),
                    CreatedAt = table.Column<DateTime>(type: "TEXT", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Tableaux", x => x.Id);
                    table.ForeignKey(
                        name: "FK_Tableaux_Categories_CategorieId",
                        column: x => x.CategorieId,
                        principalTable: "Categories",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "Classements",
                columns: table => new
                {
                    Id = table.Column<int>(type: "INTEGER", nullable: false)
                        .Annotation("Sqlite:Autoincrement", true),
                    CategorieId = table.Column<int>(type: "INTEGER", nullable: false),
                    ParticipantId = table.Column<int>(type: "INTEGER", nullable: true),
                    EquipeId = table.Column<int>(type: "INTEGER", nullable: true),
                    Position = table.Column<int>(type: "INTEGER", nullable: false),
                    Medaille = table.Column<int>(type: "INTEGER", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Classements", x => x.Id);
                    table.ForeignKey(
                        name: "FK_Classements_Categories_CategorieId",
                        column: x => x.CategorieId,
                        principalTable: "Categories",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_Classements_Equipes_EquipeId",
                        column: x => x.EquipeId,
                        principalTable: "Equipes",
                        principalColumn: "Id");
                    table.ForeignKey(
                        name: "FK_Classements_Participants_ParticipantId",
                        column: x => x.ParticipantId,
                        principalTable: "Participants",
                        principalColumn: "Id");
                });

            migrationBuilder.CreateTable(
                name: "EquipeMembres",
                columns: table => new
                {
                    Id = table.Column<int>(type: "INTEGER", nullable: false)
                        .Annotation("Sqlite:Autoincrement", true),
                    EquipeId = table.Column<int>(type: "INTEGER", nullable: false),
                    ParticipantId = table.Column<int>(type: "INTEGER", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_EquipeMembres", x => x.Id);
                    table.ForeignKey(
                        name: "FK_EquipeMembres_Equipes_EquipeId",
                        column: x => x.EquipeId,
                        principalTable: "Equipes",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_EquipeMembres_Participants_ParticipantId",
                        column: x => x.ParticipantId,
                        principalTable: "Participants",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "Inscriptions",
                columns: table => new
                {
                    Id = table.Column<int>(type: "INTEGER", nullable: false)
                        .Annotation("Sqlite:Autoincrement", true),
                    CategorieId = table.Column<int>(type: "INTEGER", nullable: false),
                    ParticipantId = table.Column<int>(type: "INTEGER", nullable: true),
                    EquipeId = table.Column<int>(type: "INTEGER", nullable: true),
                    Statut = table.Column<int>(type: "INTEGER", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Inscriptions", x => x.Id);
                    table.ForeignKey(
                        name: "FK_Inscriptions_Categories_CategorieId",
                        column: x => x.CategorieId,
                        principalTable: "Categories",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_Inscriptions_Equipes_EquipeId",
                        column: x => x.EquipeId,
                        principalTable: "Equipes",
                        principalColumn: "Id");
                    table.ForeignKey(
                        name: "FK_Inscriptions_Participants_ParticipantId",
                        column: x => x.ParticipantId,
                        principalTable: "Participants",
                        principalColumn: "Id");
                });

            migrationBuilder.CreateTable(
                name: "Combats",
                columns: table => new
                {
                    Id = table.Column<int>(type: "INTEGER", nullable: false)
                        .Annotation("Sqlite:Autoincrement", true),
                    TableauId = table.Column<int>(type: "INTEGER", nullable: false),
                    Tour = table.Column<int>(type: "INTEGER", nullable: false),
                    Aire = table.Column<string>(type: "TEXT", nullable: true),
                    CompetiteurAkaId = table.Column<int>(type: "INTEGER", nullable: true),
                    CompetiteurAoId = table.Column<int>(type: "INTEGER", nullable: true),
                    ArbitreNom = table.Column<string>(type: "TEXT", nullable: true),
                    ScoreAka = table.Column<int>(type: "INTEGER", nullable: false),
                    ScoreAo = table.Column<int>(type: "INTEGER", nullable: false),
                    SenshuCouleur = table.Column<int>(type: "INTEGER", nullable: true),
                    ModeDecision = table.Column<int>(type: "INTEGER", nullable: true),
                    VainqueurCouleur = table.Column<int>(type: "INTEGER", nullable: true),
                    DureeReelleSec = table.Column<int>(type: "INTEGER", nullable: true),
                    Statut = table.Column<int>(type: "INTEGER", nullable: false),
                    EstBye = table.Column<bool>(type: "INTEGER", nullable: false),
                    EstRepechage = table.Column<bool>(type: "INTEGER", nullable: false),
                    Moitie = table.Column<int>(type: "INTEGER", nullable: true),
                    ProchainCombatId = table.Column<int>(type: "INTEGER", nullable: true),
                    ProchainCombatCouleur = table.Column<int>(type: "INTEGER", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Combats", x => x.Id);
                    table.ForeignKey(
                        name: "FK_Combats_Combats_ProchainCombatId",
                        column: x => x.ProchainCombatId,
                        principalTable: "Combats",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_Combats_Participants_CompetiteurAkaId",
                        column: x => x.CompetiteurAkaId,
                        principalTable: "Participants",
                        principalColumn: "Id");
                    table.ForeignKey(
                        name: "FK_Combats_Participants_CompetiteurAoId",
                        column: x => x.CompetiteurAoId,
                        principalTable: "Participants",
                        principalColumn: "Id");
                    table.ForeignKey(
                        name: "FK_Combats_Tableaux_TableauId",
                        column: x => x.TableauId,
                        principalTable: "Tableaux",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "KataConfrontations",
                columns: table => new
                {
                    Id = table.Column<int>(type: "INTEGER", nullable: false)
                        .Annotation("Sqlite:Autoincrement", true),
                    TableauId = table.Column<int>(type: "INTEGER", nullable: false),
                    Tour = table.Column<int>(type: "INTEGER", nullable: false),
                    Participant1Id = table.Column<int>(type: "INTEGER", nullable: true),
                    Equipe1Id = table.Column<int>(type: "INTEGER", nullable: true),
                    Participant2Id = table.Column<int>(type: "INTEGER", nullable: true),
                    Equipe2Id = table.Column<int>(type: "INTEGER", nullable: true),
                    Kata1Id = table.Column<int>(type: "INTEGER", nullable: true),
                    Kata2Id = table.Column<int>(type: "INTEGER", nullable: true),
                    NbJuges = table.Column<int>(type: "INTEGER", nullable: false),
                    VainqueurCouleur = table.Column<int>(type: "INTEGER", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_KataConfrontations", x => x.Id);
                    table.ForeignKey(
                        name: "FK_KataConfrontations_Equipes_Equipe1Id",
                        column: x => x.Equipe1Id,
                        principalTable: "Equipes",
                        principalColumn: "Id");
                    table.ForeignKey(
                        name: "FK_KataConfrontations_Equipes_Equipe2Id",
                        column: x => x.Equipe2Id,
                        principalTable: "Equipes",
                        principalColumn: "Id");
                    table.ForeignKey(
                        name: "FK_KataConfrontations_Katas_Kata1Id",
                        column: x => x.Kata1Id,
                        principalTable: "Katas",
                        principalColumn: "Id");
                    table.ForeignKey(
                        name: "FK_KataConfrontations_Katas_Kata2Id",
                        column: x => x.Kata2Id,
                        principalTable: "Katas",
                        principalColumn: "Id");
                    table.ForeignKey(
                        name: "FK_KataConfrontations_Participants_Participant1Id",
                        column: x => x.Participant1Id,
                        principalTable: "Participants",
                        principalColumn: "Id");
                    table.ForeignKey(
                        name: "FK_KataConfrontations_Participants_Participant2Id",
                        column: x => x.Participant2Id,
                        principalTable: "Participants",
                        principalColumn: "Id");
                    table.ForeignKey(
                        name: "FK_KataConfrontations_Tableaux_TableauId",
                        column: x => x.TableauId,
                        principalTable: "Tableaux",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "EvenementsCombat",
                columns: table => new
                {
                    Id = table.Column<int>(type: "INTEGER", nullable: false)
                        .Annotation("Sqlite:Autoincrement", true),
                    CombatId = table.Column<int>(type: "INTEGER", nullable: false),
                    TempsCombat = table.Column<TimeSpan>(type: "TEXT", nullable: false),
                    Horodatage = table.Column<DateTime>(type: "TEXT", nullable: false),
                    Type = table.Column<int>(type: "INTEGER", nullable: false),
                    Couleur = table.Column<int>(type: "INTEGER", nullable: false),
                    Points = table.Column<int>(type: "INTEGER", nullable: true),
                    Penalite = table.Column<int>(type: "INTEGER", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_EvenementsCombat", x => x.Id);
                    table.ForeignKey(
                        name: "FK_EvenementsCombat_Combats_CombatId",
                        column: x => x.CombatId,
                        principalTable: "Combats",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "VotesJuges",
                columns: table => new
                {
                    Id = table.Column<int>(type: "INTEGER", nullable: false)
                        .Annotation("Sqlite:Autoincrement", true),
                    ConfrontationId = table.Column<int>(type: "INTEGER", nullable: false),
                    JugeNumero = table.Column<int>(type: "INTEGER", nullable: false),
                    VoteCouleur = table.Column<int>(type: "INTEGER", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_VotesJuges", x => x.Id);
                    table.ForeignKey(
                        name: "FK_VotesJuges_KataConfrontations_ConfrontationId",
                        column: x => x.ConfrontationId,
                        principalTable: "KataConfrontations",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.InsertData(
                table: "Katas",
                columns: new[] { "Id", "Actif", "Nom" },
                values: new object[,]
                {
                    { 1, true, "Heian Shodan" },
                    { 2, true, "Heian Nidan" },
                    { 3, true, "Heian Sandan" },
                    { 4, true, "Heian Yondan" },
                    { 5, true, "Heian Godan" },
                    { 6, true, "Tekki Shodan" },
                    { 7, true, "Tekki Nidan" },
                    { 8, true, "Tekki Sandan" },
                    { 9, true, "Bassai Dai" },
                    { 10, true, "Bassai Sho" },
                    { 11, true, "Kanku Dai" },
                    { 12, true, "Kanku Sho" },
                    { 13, true, "Empi" },
                    { 14, true, "Jion" },
                    { 15, true, "Jitte" },
                    { 16, true, "Hangetsu" },
                    { 17, true, "Gankaku" },
                    { 18, true, "Nijushiho" },
                    { 19, true, "Chinte" },
                    { 20, true, "Sochin" },
                    { 21, true, "Meikyo" },
                    { 22, true, "Unsu" },
                    { 23, true, "Wankan" },
                    { 24, true, "Jiin" }
                });

            migrationBuilder.CreateIndex(
                name: "IX_Categories_CompetitionId",
                table: "Categories",
                column: "CompetitionId");

            migrationBuilder.CreateIndex(
                name: "IX_Classements_CategorieId",
                table: "Classements",
                column: "CategorieId");

            migrationBuilder.CreateIndex(
                name: "IX_Classements_EquipeId",
                table: "Classements",
                column: "EquipeId");

            migrationBuilder.CreateIndex(
                name: "IX_Classements_ParticipantId",
                table: "Classements",
                column: "ParticipantId");

            migrationBuilder.CreateIndex(
                name: "IX_Combats_CompetiteurAkaId",
                table: "Combats",
                column: "CompetiteurAkaId");

            migrationBuilder.CreateIndex(
                name: "IX_Combats_CompetiteurAoId",
                table: "Combats",
                column: "CompetiteurAoId");

            migrationBuilder.CreateIndex(
                name: "IX_Combats_ProchainCombatId",
                table: "Combats",
                column: "ProchainCombatId");

            migrationBuilder.CreateIndex(
                name: "IX_Combats_TableauId",
                table: "Combats",
                column: "TableauId");

            migrationBuilder.CreateIndex(
                name: "IX_EquipeMembres_EquipeId",
                table: "EquipeMembres",
                column: "EquipeId");

            migrationBuilder.CreateIndex(
                name: "IX_EquipeMembres_ParticipantId",
                table: "EquipeMembres",
                column: "ParticipantId");

            migrationBuilder.CreateIndex(
                name: "IX_Equipes_CategorieId",
                table: "Equipes",
                column: "CategorieId");

            migrationBuilder.CreateIndex(
                name: "IX_Equipes_ClubId",
                table: "Equipes",
                column: "ClubId");

            migrationBuilder.CreateIndex(
                name: "IX_EvenementsCombat_CombatId",
                table: "EvenementsCombat",
                column: "CombatId");

            migrationBuilder.CreateIndex(
                name: "IX_Inscriptions_CategorieId",
                table: "Inscriptions",
                column: "CategorieId");

            migrationBuilder.CreateIndex(
                name: "IX_Inscriptions_EquipeId",
                table: "Inscriptions",
                column: "EquipeId");

            migrationBuilder.CreateIndex(
                name: "IX_Inscriptions_ParticipantId",
                table: "Inscriptions",
                column: "ParticipantId");

            migrationBuilder.CreateIndex(
                name: "IX_KataConfrontations_Equipe1Id",
                table: "KataConfrontations",
                column: "Equipe1Id");

            migrationBuilder.CreateIndex(
                name: "IX_KataConfrontations_Equipe2Id",
                table: "KataConfrontations",
                column: "Equipe2Id");

            migrationBuilder.CreateIndex(
                name: "IX_KataConfrontations_Kata1Id",
                table: "KataConfrontations",
                column: "Kata1Id");

            migrationBuilder.CreateIndex(
                name: "IX_KataConfrontations_Kata2Id",
                table: "KataConfrontations",
                column: "Kata2Id");

            migrationBuilder.CreateIndex(
                name: "IX_KataConfrontations_Participant1Id",
                table: "KataConfrontations",
                column: "Participant1Id");

            migrationBuilder.CreateIndex(
                name: "IX_KataConfrontations_Participant2Id",
                table: "KataConfrontations",
                column: "Participant2Id");

            migrationBuilder.CreateIndex(
                name: "IX_KataConfrontations_TableauId",
                table: "KataConfrontations",
                column: "TableauId");

            migrationBuilder.CreateIndex(
                name: "IX_Participants_ClubId",
                table: "Participants",
                column: "ClubId");

            migrationBuilder.CreateIndex(
                name: "IX_Tableaux_CategorieId",
                table: "Tableaux",
                column: "CategorieId");

            migrationBuilder.CreateIndex(
                name: "IX_VotesJuges_ConfrontationId",
                table: "VotesJuges",
                column: "ConfrontationId");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "AuditLogs");

            migrationBuilder.DropTable(
                name: "Classements");

            migrationBuilder.DropTable(
                name: "EquipeMembres");

            migrationBuilder.DropTable(
                name: "EvenementsCombat");

            migrationBuilder.DropTable(
                name: "Inscriptions");

            migrationBuilder.DropTable(
                name: "VotesJuges");

            migrationBuilder.DropTable(
                name: "Combats");

            migrationBuilder.DropTable(
                name: "KataConfrontations");

            migrationBuilder.DropTable(
                name: "Equipes");

            migrationBuilder.DropTable(
                name: "Katas");

            migrationBuilder.DropTable(
                name: "Participants");

            migrationBuilder.DropTable(
                name: "Tableaux");

            migrationBuilder.DropTable(
                name: "Clubs");

            migrationBuilder.DropTable(
                name: "Categories");

            migrationBuilder.DropTable(
                name: "Competitions");
        }
    }
}
