using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace SafeBand.api.Migrations
{
    /// <inheritdoc />
    public partial class GuardarAlumnoEnLecturas : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<int>(
                name: "AlumnoId",
                table: "LecturasBle",
                type: "int",
                nullable: true);

            migrationBuilder.CreateIndex(
                name: "IX_LecturasBle_AlumnoId_Timestamp",
                table: "LecturasBle",
                columns: new[] { "AlumnoId", "Timestamp" });

            migrationBuilder.AddForeignKey(
                name: "FK_LecturasBle_Alumnos_AlumnoId",
                table: "LecturasBle",
                column: "AlumnoId",
                principalTable: "Alumnos",
                principalColumn: "Id",
                onDelete: ReferentialAction.Restrict);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_LecturasBle_Alumnos_AlumnoId",
                table: "LecturasBle");

            migrationBuilder.DropIndex(
                name: "IX_LecturasBle_AlumnoId_Timestamp",
                table: "LecturasBle");

            migrationBuilder.DropColumn(
                name: "AlumnoId",
                table: "LecturasBle");
        }
    }
}
