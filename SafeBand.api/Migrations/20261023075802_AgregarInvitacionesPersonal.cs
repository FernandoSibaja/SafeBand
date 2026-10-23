using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace SafeBand.api.Migrations
{
    /// <inheritdoc />
    public partial class AgregarInvitacionesPersonal : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AlterColumn<int>(
                name: "TutorId",
                table: "Invitaciones",
                type: "int",
                nullable: true,
                oldClrType: typeof(int),
                oldType: "int");

            migrationBuilder.AddColumn<string>(
                name: "UsuarioId",
                table: "Invitaciones",
                type: "nvarchar(450)",
                nullable: true);

            migrationBuilder.CreateIndex(
                name: "IX_Invitaciones_UsuarioId",
                table: "Invitaciones",
                column: "UsuarioId");

            migrationBuilder.AddCheckConstraint(
                name: "CK_Invitaciones_TutorOUsuario",
                table: "Invitaciones",
                sql: "([TutorId] IS NULL AND [UsuarioId] IS NOT NULL) OR ([TutorId] IS NOT NULL AND [UsuarioId] IS NULL)");

            migrationBuilder.AddForeignKey(
                name: "FK_Invitaciones_AspNetUsers_UsuarioId",
                table: "Invitaciones",
                column: "UsuarioId",
                principalTable: "AspNetUsers",
                principalColumn: "Id",
                onDelete: ReferentialAction.Restrict);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_Invitaciones_AspNetUsers_UsuarioId",
                table: "Invitaciones");

            migrationBuilder.DropIndex(
                name: "IX_Invitaciones_UsuarioId",
                table: "Invitaciones");

            migrationBuilder.DropCheckConstraint(
                name: "CK_Invitaciones_TutorOUsuario",
                table: "Invitaciones");

            migrationBuilder.DropColumn(
                name: "UsuarioId",
                table: "Invitaciones");

            migrationBuilder.AlterColumn<int>(
                name: "TutorId",
                table: "Invitaciones",
                type: "int",
                nullable: false,
                defaultValue: 0,
                oldClrType: typeof(int),
                oldType: "int",
                oldNullable: true);
        }
    }
}
