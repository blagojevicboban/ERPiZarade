using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace ERPiZaradeData.Migrations
{
    /// <inheritdoc />
    public partial class Faza2_PoreskeOlaksice : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "PoreskeOlaksice",
                columns: table => new
                {
                    PoreskaOlaksicaId = table.Column<int>(type: "INTEGER", nullable: false)
                        .Annotation("Sqlite:Autoincrement", true),
                    Sifra = table.Column<string>(type: "TEXT", maxLength: 2, nullable: false),
                    Naziv = table.Column<string>(type: "TEXT", maxLength: 100, nullable: false),
                    PravniOsnov = table.Column<string>(type: "TEXT", maxLength: 100, nullable: false),
                    Mehanizam = table.Column<int>(type: "INTEGER", nullable: false),
                    ProcenatPoreza = table.Column<decimal>(type: "decimal(6,2)", nullable: false),
                    ProcenatDoprinosa = table.Column<decimal>(type: "decimal(6,2)", nullable: false),
                    VaziOd = table.Column<DateTime>(type: "TEXT", nullable: true),
                    VaziDo = table.Column<DateTime>(type: "TEXT", nullable: true),
                    Aktivna = table.Column<bool>(type: "INTEGER", nullable: false),
                    Napomena = table.Column<string>(type: "TEXT", maxLength: 300, nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_PoreskeOlaksice", x => x.PoreskaOlaksicaId);
                });

            migrationBuilder.CreateTable(
                name: "OlaksicaMfp",
                columns: table => new
                {
                    OlaksicaMfpId = table.Column<int>(type: "INTEGER", nullable: false)
                        .Annotation("Sqlite:Autoincrement", true),
                    PoreskaOlaksicaId = table.Column<int>(type: "INTEGER", nullable: false),
                    Oznaka = table.Column<string>(type: "TEXT", maxLength: 10, nullable: false),
                    Izvor = table.Column<int>(type: "INTEGER", nullable: false),
                    FiksnaVrednost = table.Column<decimal>(type: "decimal(14,2)", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_OlaksicaMfp", x => x.OlaksicaMfpId);
                    table.ForeignKey(
                        name: "FK_OlaksicaMfp_PoreskeOlaksice_PoreskaOlaksicaId",
                        column: x => x.PoreskaOlaksicaId,
                        principalTable: "PoreskeOlaksice",
                        principalColumn: "PoreskaOlaksicaId",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "IX_OlaksicaMfp_PoreskaOlaksicaId_Oznaka",
                table: "OlaksicaMfp",
                columns: new[] { "PoreskaOlaksicaId", "Oznaka" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_PoreskeOlaksice_Sifra",
                table: "PoreskeOlaksice",
                column: "Sifra",
                unique: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "OlaksicaMfp");

            migrationBuilder.DropTable(
                name: "PoreskeOlaksice");
        }
    }
}
