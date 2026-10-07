using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace SafeBand.api.Migrations
{
    /// <inheritdoc />
    public partial class AgregarInvitaciones : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<DateTime>(
                name: "AvisoPrivacidadAceptado",
                table: "AspNetUsers",
                type: "datetime2",
                nullable: true);

            migrationBuilder.CreateTable(
                name: "Invitaciones",
                columns: table => new
                {
                    Id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    TutorId = table.Column<int>(type: "int", nullable: false),
                    CodigoHash = table.Column<string>(type: "nvarchar(200)", maxLength: 200, nullable: false),
                    Creada = table.Column<DateTime>(type: "datetime2", nullable: false),
                    Expira = table.Column<DateTime>(type: "datetime2", nullable: false),
                    UsadaEn = table.Column<DateTime>(type: "datetime2", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Invitaciones", x => x.Id);
                    table.ForeignKey(
                        name: "FK_Invitaciones_Tutores_TutorId",
                        column: x => x.TutorId,
                        principalTable: "Tutores",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "IX_Invitaciones_TutorId",
                table: "Invitaciones",
                column: "TutorId");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "Invitaciones");

            migrationBuilder.DropColumn(
                name: "AvisoPrivacidadAceptado",
                table: "AspNetUsers");
        }
    }
}
