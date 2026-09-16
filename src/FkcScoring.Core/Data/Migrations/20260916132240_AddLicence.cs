using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace FkcScoring.Core.Data.Migrations
{
    /// <inheritdoc />
    public partial class AddLicence : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "LicenceCle",
                table: "Parametres",
                type: "TEXT",
                nullable: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "LicenceCle",
                table: "Parametres");
        }
    }
}
