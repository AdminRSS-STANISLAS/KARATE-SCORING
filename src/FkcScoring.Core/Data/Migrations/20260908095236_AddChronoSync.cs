using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace FkcScoring.Core.Data.Migrations
{
    /// <inheritdoc />
    public partial class AddChronoSync : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<DateTime>(
                name: "ChronoDemarreLeUtc",
                table: "Combats",
                type: "TEXT",
                nullable: true);

            migrationBuilder.AddColumn<int>(
                name: "ChronoRestantMs",
                table: "Combats",
                type: "INTEGER",
                nullable: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "ChronoDemarreLeUtc",
                table: "Combats");

            migrationBuilder.DropColumn(
                name: "ChronoRestantMs",
                table: "Combats");
        }
    }
}
