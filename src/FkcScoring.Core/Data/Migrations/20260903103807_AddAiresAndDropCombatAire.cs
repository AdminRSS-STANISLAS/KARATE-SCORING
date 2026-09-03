using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace FkcScoring.Core.Data.Migrations
{
    /// <inheritdoc />
    public partial class AddAiresAndDropCombatAire : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "Aire",
                table: "Combats");

            migrationBuilder.AddColumn<int>(
                name: "AireId",
                table: "Tableaux",
                type: "INTEGER",
                nullable: true);

            migrationBuilder.CreateTable(
                name: "Aires",
                columns: table => new
                {
                    Id = table.Column<int>(type: "INTEGER", nullable: false)
                        .Annotation("Sqlite:Autoincrement", true),
                    CompetitionId = table.Column<int>(type: "INTEGER", nullable: false),
                    Nom = table.Column<string>(type: "TEXT", nullable: false),
                    Ordre = table.Column<int>(type: "INTEGER", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Aires", x => x.Id);
                    table.ForeignKey(
                        name: "FK_Aires_Competitions_CompetitionId",
                        column: x => x.CompetitionId,
                        principalTable: "Competitions",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "IX_Tableaux_AireId",
                table: "Tableaux",
                column: "AireId");

            migrationBuilder.CreateIndex(
                name: "IX_Aires_CompetitionId",
                table: "Aires",
                column: "CompetitionId");

            migrationBuilder.AddForeignKey(
                name: "FK_Tableaux_Aires_AireId",
                table: "Tableaux",
                column: "AireId",
                principalTable: "Aires",
                principalColumn: "Id",
                onDelete: ReferentialAction.SetNull);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_Tableaux_Aires_AireId",
                table: "Tableaux");

            migrationBuilder.DropTable(
                name: "Aires");

            migrationBuilder.DropIndex(
                name: "IX_Tableaux_AireId",
                table: "Tableaux");

            migrationBuilder.DropColumn(
                name: "AireId",
                table: "Tableaux");

            migrationBuilder.AddColumn<string>(
                name: "Aire",
                table: "Combats",
                type: "TEXT",
                nullable: true);
        }
    }
}
