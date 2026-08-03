using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace ERPiZaradeData.Migrations
{
    /// <inheritdoc />
    public partial class Faza2_Isplate : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<int>(
                name: "IsplataId",
                table: "ObracunVerzije",
                type: "INTEGER",
                nullable: true);

            migrationBuilder.AddColumn<int>(
                name: "IsplataId",
                table: "ObracuniPlata",
                type: "INTEGER",
                nullable: true);

            migrationBuilder.CreateTable(
                name: "Isplate",
                columns: table => new
                {
                    IsplataId = table.Column<int>(type: "INTEGER", nullable: false)
                        .Annotation("Sqlite:Autoincrement", true),
                    Godina = table.Column<int>(type: "INTEGER", nullable: false),
                    Mesec = table.Column<int>(type: "INTEGER", nullable: false),
                    RedniBroj = table.Column<int>(type: "INTEGER", nullable: false),
                    Vrsta = table.Column<int>(type: "INTEGER", nullable: false),
                    Opis = table.Column<string>(type: "TEXT", maxLength: 80, nullable: false),
                    DatumIsplate = table.Column<DateTime>(type: "TEXT", nullable: false),
                    DatumKreiranja = table.Column<DateTime>(type: "TEXT", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Isplate", x => x.IsplataId);
                });

            migrationBuilder.CreateIndex(
                name: "IX_ObracuniPlata_IsplataId",
                table: "ObracuniPlata",
                column: "IsplataId");

            migrationBuilder.CreateIndex(
                name: "IX_Isplate_Godina_Mesec_RedniBroj",
                table: "Isplate",
                columns: new[] { "Godina", "Mesec", "RedniBroj" },
                unique: true);

            migrationBuilder.AddForeignKey(
                name: "FK_ObracuniPlata_Isplate_IsplataId",
                table: "ObracuniPlata",
                column: "IsplataId",
                principalTable: "Isplate",
                principalColumn: "IsplataId",
                onDelete: ReferentialAction.Restrict);

            // Svaki zatečeni period je imao tačno jednu isplatu, pa je i dobija: prvu, vrste
            // „konačna zarada", sa datumom poslednjeg dana meseca. Bez ovoga bi ekran isplata
            // za sve ranije mesece bio prazan, iako obračuni u njima postoje.
            migrationBuilder.Sql(@"
                INSERT INTO Isplate (Godina, Mesec, RedniBroj, Vrsta, Opis, DatumIsplate, DatumKreiranja)
                SELECT DISTINCT o.Godina, o.Mesec, 1, 0, '',
                       datetime(printf('%04d-%02d-01', o.Godina, o.Mesec), '+1 month', '-1 day'),
                       datetime('now', 'localtime')
                FROM ObracuniPlata o
                WHERE o.Godina > 0 AND o.Mesec BETWEEN 1 AND 12;");

            migrationBuilder.Sql(@"
                UPDATE ObracuniPlata
                SET IsplataId = (
                    SELECT i.IsplataId FROM Isplate i
                    WHERE i.Godina = ObracuniPlata.Godina
                      AND i.Mesec = ObracuniPlata.Mesec
                      AND i.RedniBroj = 1)
                WHERE IsplataId IS NULL;");

            // Arhivirane verzije ostaju sa `IsplataId` NULL — nastale su pre nego što je
            // isplata postojala, a NULL je i inače prva isplata perioda.
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_ObracuniPlata_Isplate_IsplataId",
                table: "ObracuniPlata");

            migrationBuilder.DropTable(
                name: "Isplate");

            migrationBuilder.DropIndex(
                name: "IX_ObracuniPlata_IsplataId",
                table: "ObracuniPlata");

            migrationBuilder.DropColumn(
                name: "IsplataId",
                table: "ObracunVerzije");

            migrationBuilder.DropColumn(
                name: "IsplataId",
                table: "ObracuniPlata");
        }
    }
}
