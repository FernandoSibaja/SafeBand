using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace SafeBand.api.Migrations
{
    /// <inheritdoc />
    public partial class AgregarPlanoZonas : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<double>(
                name: "PlanoAlto",
                table: "Zonas",
                type: "float",
                nullable: true);

            migrationBuilder.AddColumn<double>(
                name: "PlanoAncho",
                table: "Zonas",
                type: "float",
                nullable: true);

            migrationBuilder.AddColumn<double>(
                name: "PlanoX",
                table: "Zonas",
                type: "float",
                nullable: true);

            migrationBuilder.AddColumn<double>(
                name: "PlanoY",
                table: "Zonas",
                type: "float",
                nullable: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "PlanoAlto",
                table: "Zonas");

            migrationBuilder.DropColumn(
                name: "PlanoAncho",
                table: "Zonas");

            migrationBuilder.DropColumn(
                name: "PlanoX",
                table: "Zonas");

            migrationBuilder.DropColumn(
                name: "PlanoY",
                table: "Zonas");
        }
    }
}
