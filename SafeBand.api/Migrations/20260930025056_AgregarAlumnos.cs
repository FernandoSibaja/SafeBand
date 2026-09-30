using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace SafeBand.api.Migrations
{
    /// <inheritdoc />
    public partial class AgregarAlumnos : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<int>(
                name: "AlumnoId",
                table: "Pulseras",
                type: "int",
                nullable: true);

            migrationBuilder.CreateTable(
                name: "Alumnos",
                columns: table => new
                {
                    Id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    Nombres = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: false),
                    ApellidoPaterno = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: false),
                    ApellidoMaterno = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: true),
                    FechaNacimiento = table.Column<DateOnly>(type: "date", nullable: true),
                    Matricula = table.Column<string>(type: "nvarchar(30)", maxLength: 30, nullable: true),
                    Activo = table.Column<bool>(type: "bit", nullable: false),
                    FechaAlta = table.Column<DateTime>(type: "datetime2", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Alumnos", x => x.Id);
                });

            migrationBuilder.CreateIndex(
                name: "IX_Pulseras_AlumnoId",
                table: "Pulseras",
                column: "AlumnoId");

            migrationBuilder.CreateIndex(
                name: "IX_Alumnos_Matricula",
                table: "Alumnos",
                column: "Matricula",
                unique: true,
                filter: "[Matricula] IS NOT NULL");

            migrationBuilder.AddForeignKey(
                name: "FK_Pulseras_Alumnos_AlumnoId",
                table: "Pulseras",
                column: "AlumnoId",
                principalTable: "Alumnos",
                principalColumn: "Id",
                onDelete: ReferentialAction.SetNull);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_Pulseras_Alumnos_AlumnoId",
                table: "Pulseras");

            migrationBuilder.DropTable(
                name: "Alumnos");

            migrationBuilder.DropIndex(
                name: "IX_Pulseras_AlumnoId",
                table: "Pulseras");

            migrationBuilder.DropColumn(
                name: "AlumnoId",
                table: "Pulseras");
        }
    }
}
