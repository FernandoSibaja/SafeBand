using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace SafeBand.api.Migrations
{
    /// <inheritdoc />
    public partial class Fase1_Inicial : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "Pulseras",
                columns: table => new
                {
                    Id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    IdentificadorBle = table.Column<string>(type: "nvarchar(50)", maxLength: 50, nullable: false),
                    UidNfc = table.Column<string>(type: "nvarchar(32)", maxLength: 32, nullable: true),
                    Activa = table.Column<bool>(type: "bit", nullable: false),
                    FechaAlta = table.Column<DateTime>(type: "datetime2", nullable: false),
                    UltimaLectura = table.Column<DateTime>(type: "datetime2", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Pulseras", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "Zonas",
                columns: table => new
                {
                    Id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    Nombre = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: false),
                    Tipo = table.Column<string>(type: "nvarchar(20)", maxLength: 20, nullable: false),
                    Activa = table.Column<bool>(type: "bit", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Zonas", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "Nodos",
                columns: table => new
                {
                    Id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    Codigo = table.Column<string>(type: "nvarchar(50)", maxLength: 50, nullable: false),
                    Descripcion = table.Column<string>(type: "nvarchar(200)", maxLength: 200, nullable: true),
                    ZonaId = table.Column<int>(type: "int", nullable: false),
                    UmbralRssi = table.Column<int>(type: "int", nullable: false),
                    EsPortatil = table.Column<bool>(type: "bit", nullable: false),
                    Activo = table.Column<bool>(type: "bit", nullable: false),
                    UltimoContacto = table.Column<DateTime>(type: "datetime2", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Nodos", x => x.Id);
                    table.ForeignKey(
                        name: "FK_Nodos_Zonas_ZonaId",
                        column: x => x.ZonaId,
                        principalTable: "Zonas",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "LecturasBle",
                columns: table => new
                {
                    Id = table.Column<long>(type: "bigint", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    NodoId = table.Column<int>(type: "int", nullable: false),
                    PulseraId = table.Column<int>(type: "int", nullable: false),
                    Rssi = table.Column<int>(type: "int", nullable: false),
                    Puesta = table.Column<bool>(type: "bit", nullable: false),
                    Sos = table.Column<bool>(type: "bit", nullable: false),
                    BateriaBaja = table.Column<bool>(type: "bit", nullable: false),
                    Timestamp = table.Column<DateTime>(type: "datetime2", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_LecturasBle", x => x.Id);
                    table.ForeignKey(
                        name: "FK_LecturasBle_Nodos_NodoId",
                        column: x => x.NodoId,
                        principalTable: "Nodos",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_LecturasBle_Pulseras_PulseraId",
                        column: x => x.PulseraId,
                        principalTable: "Pulseras",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateIndex(
                name: "IX_LecturasBle_NodoId",
                table: "LecturasBle",
                column: "NodoId");

            migrationBuilder.CreateIndex(
                name: "IX_LecturasBle_PulseraId_Timestamp",
                table: "LecturasBle",
                columns: new[] { "PulseraId", "Timestamp" });

            migrationBuilder.CreateIndex(
                name: "IX_LecturasBle_Timestamp",
                table: "LecturasBle",
                column: "Timestamp");

            migrationBuilder.CreateIndex(
                name: "IX_Nodos_Codigo",
                table: "Nodos",
                column: "Codigo",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_Nodos_ZonaId",
                table: "Nodos",
                column: "ZonaId");

            migrationBuilder.CreateIndex(
                name: "IX_Pulseras_IdentificadorBle",
                table: "Pulseras",
                column: "IdentificadorBle",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_Pulseras_UidNfc",
                table: "Pulseras",
                column: "UidNfc",
                unique: true,
                filter: "[UidNfc] IS NOT NULL");

            migrationBuilder.CreateIndex(
                name: "IX_Zonas_Nombre",
                table: "Zonas",
                column: "Nombre",
                unique: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "LecturasBle");

            migrationBuilder.DropTable(
                name: "Nodos");

            migrationBuilder.DropTable(
                name: "Pulseras");

            migrationBuilder.DropTable(
                name: "Zonas");
        }
    }
}
