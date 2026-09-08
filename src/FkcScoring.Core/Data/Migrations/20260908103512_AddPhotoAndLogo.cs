using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace FkcScoring.Core.Data.Migrations
{
    /// <inheritdoc />
    public partial class AddPhotoAndLogo : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "PhotoExtension",
                table: "Participants",
                type: "TEXT",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "LogoExtension",
                table: "Clubs",
                type: "TEXT",
                nullable: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "PhotoExtension",
                table: "Participants");

            migrationBuilder.DropColumn(
                name: "LogoExtension",
                table: "Clubs");
        }
    }
}
