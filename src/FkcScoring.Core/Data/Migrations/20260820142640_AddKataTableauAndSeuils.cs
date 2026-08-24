using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace FkcScoring.Core.Data.Migrations
{
    /// <inheritdoc />
    public partial class AddKataTableauAndSeuils : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<bool>(
                name: "EstBye",
                table: "KataConfrontations",
                type: "INTEGER",
                nullable: false,
                defaultValue: false);

            migrationBuilder.AddColumn<bool>(
                name: "EstRepechage",
                table: "KataConfrontations",
                type: "INTEGER",
                nullable: false,
                defaultValue: false);

            migrationBuilder.AddColumn<int>(
                name: "Moitie",
                table: "KataConfrontations",
                type: "INTEGER",
                nullable: true);

            migrationBuilder.AddColumn<int>(
                name: "ProchainConfrontationCouleur",
                table: "KataConfrontations",
                type: "INTEGER",
                nullable: true);

            migrationBuilder.AddColumn<int>(
                name: "ProchainConfrontationId",
                table: "KataConfrontations",
                type: "INTEGER",
                nullable: true);

            migrationBuilder.AddColumn<int>(
                name: "Statut",
                table: "KataConfrontations",
                type: "INTEGER",
                nullable: false,
                defaultValue: 0);

            migrationBuilder.AddColumn<int>(
                name: "SeuilPoulePuisElimination",
                table: "Competitions",
                type: "INTEGER",
                nullable: false,
                defaultValue: 0);

            migrationBuilder.AddColumn<int>(
                name: "SeuilPouleUnique",
                table: "Competitions",
                type: "INTEGER",
                nullable: false,
                defaultValue: 0);

            migrationBuilder.CreateIndex(
                name: "IX_KataConfrontations_ProchainConfrontationId",
                table: "KataConfrontations",
                column: "ProchainConfrontationId");

            migrationBuilder.AddForeignKey(
                name: "FK_KataConfrontations_KataConfrontations_ProchainConfrontationId",
                table: "KataConfrontations",
                column: "ProchainConfrontationId",
                principalTable: "KataConfrontations",
                principalColumn: "Id",
                onDelete: ReferentialAction.Restrict);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_KataConfrontations_KataConfrontations_ProchainConfrontationId",
                table: "KataConfrontations");

            migrationBuilder.DropIndex(
                name: "IX_KataConfrontations_ProchainConfrontationId",
                table: "KataConfrontations");

            migrationBuilder.DropColumn(
                name: "EstBye",
                table: "KataConfrontations");

            migrationBuilder.DropColumn(
                name: "EstRepechage",
                table: "KataConfrontations");

            migrationBuilder.DropColumn(
                name: "Moitie",
                table: "KataConfrontations");

            migrationBuilder.DropColumn(
                name: "ProchainConfrontationCouleur",
                table: "KataConfrontations");

            migrationBuilder.DropColumn(
                name: "ProchainConfrontationId",
                table: "KataConfrontations");

            migrationBuilder.DropColumn(
                name: "Statut",
                table: "KataConfrontations");

            migrationBuilder.DropColumn(
                name: "SeuilPoulePuisElimination",
                table: "Competitions");

            migrationBuilder.DropColumn(
                name: "SeuilPouleUnique",
                table: "Competitions");
        }
    }
}
