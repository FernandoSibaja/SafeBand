using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace SafeBand.api.Migrations
{
    /// <inheritdoc />
    public partial class AgregarTutores : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "Tutores",
                columns: table => new
                {
                    Id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    Nombres = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: false),
                    ApellidoPaterno = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: false),
                    ApellidoMaterno = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: true),
                    Email = table.Column<string>(type: "nvarchar(200)", maxLength: 200, nullable: false),
                    Telefono = table.Column<string>(type: "nvarchar(20)", maxLength: 20, nullable: true),
                    Activo = table.Column<bool>(type: "bit", nullable: false),
                    FechaAlta = table.Column<DateTime>(type: "datetime2", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Tutores", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "TutoresAlumnos",
                columns: table => new
                {
                    TutorId = table.Column<int>(type: "int", nullable: false),
                    AlumnoId = table.Column<int>(type: "int", nullable: false),
                    Parentesco = table.Column<string>(type: "nvarchar(50)", maxLength: 50, nullable: true),
                    EsContactoPrincipal = table.Column<bool>(type: "bit", nullable: false),
                    PuedeRecoger = table.Column<bool>(type: "bit", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_TutoresAlumnos", x => new { x.TutorId, x.AlumnoId });
                    table.ForeignKey(
                        name: "FK_TutoresAlumnos_Alumnos_AlumnoId",
                        column: x => x.AlumnoId,
                        principalTable: "Alumnos",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_TutoresAlumnos_Tutores_TutorId",
                        column: x => x.TutorId,
                        principalTable: "Tutores",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "IX_Tutores_Email",
                table: "Tutores",
                column: "Email",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_TutoresAlumnos_AlumnoId",
                table: "TutoresAlumnos",
                column: "AlumnoId");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "TutoresAlumnos");

            migrationBuilder.DropTable(
                name: "Tutores");
        }
    }
}
