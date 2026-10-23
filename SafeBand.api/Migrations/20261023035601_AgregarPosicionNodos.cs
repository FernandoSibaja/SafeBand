using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace SafeBand.api.Migrations
{
    /// <inheritdoc />
    public partial class AgregarPosicionNodos : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<double>(
                name: "PlanoX",
                table: "Nodos",
                type: "float",
                nullable: true);

            migrationBuilder.AddColumn<double>(
                name: "PlanoY",
                table: "Nodos",
                type: "float",
                nullable: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "PlanoX",
                table: "Nodos");

            migrationBuilder.DropColumn(
                name: "PlanoY",
                table: "Nodos");
        }
    }
}
