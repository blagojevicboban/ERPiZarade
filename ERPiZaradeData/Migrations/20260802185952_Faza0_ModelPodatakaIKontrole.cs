using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace ERPiZaradeData.Migrations
{
    /// <inheritdoc />
    public partial class Faza0_ModelPodatakaIKontrole : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "Zakljucen",
                table: "ObracuniPlata");

            migrationBuilder.AddColumn<string>(
                name: "Email",
                table: "Radnici",
                type: "TEXT",
                maxLength: 120,
                nullable: false,
                defaultValue: "");

            migrationBuilder.AddColumn<DateTime>(
                name: "OlaksicaVaziDo",
                table: "Radnici",
                type: "TEXT",
                nullable: true);

            migrationBuilder.AddColumn<decimal>(
                name: "ProcenatPovracajaDoprinosa",
                table: "Radnici",
                type: "decimal(6,2)",
                nullable: false,
                defaultValue: 0m);

            migrationBuilder.AddColumn<decimal>(
                name: "ProcenatPovracajaPoreza",
                table: "Radnici",
                type: "decimal(6,2)",
                nullable: false,
                defaultValue: 0m);

            migrationBuilder.AddColumn<string>(
                name: "SifraMestaTroska",
                table: "Radnici",
                type: "TEXT",
                maxLength: 20,
                nullable: false,
                defaultValue: "");

            migrationBuilder.AddColumn<string>(
                name: "ModelPozivaNaBroj",
                table: "Krediti",
                type: "TEXT",
                maxLength: 2,
                nullable: false,
                defaultValue: "");

            migrationBuilder.AddColumn<string>(
                name: "PozivNaBroj",
                table: "Krediti",
                type: "TEXT",
                maxLength: 25,
                nullable: false,
                defaultValue: "");

            migrationBuilder.AddColumn<string>(
                name: "PrimalacNaziv",
                table: "Krediti",
                type: "TEXT",
                maxLength: 60,
                nullable: false,
                defaultValue: "");

            migrationBuilder.AddColumn<string>(
                name: "PrimalacRacun",
                table: "Krediti",
                type: "TEXT",
                maxLength: 25,
                nullable: false,
                defaultValue: "");

            migrationBuilder.AddColumn<int>(
                name: "RedosledNaplate",
                table: "Krediti",
                type: "INTEGER",
                nullable: false,
                defaultValue: 0);

            migrationBuilder.AddColumn<int>(
                name: "Tip",
                table: "Krediti",
                type: "INTEGER",
                nullable: false,
                defaultValue: 0);

            migrationBuilder.AddColumn<string>(
                name: "SifraOpstine",
                table: "Firme",
                type: "TEXT",
                maxLength: 3,
                nullable: false,
                defaultValue: "");

            migrationBuilder.CreateTable(
                name: "ObracunAuditi",
                columns: table => new
                {
                    ObracunAuditId = table.Column<int>(type: "INTEGER", nullable: false)
                        .Annotation("Sqlite:Autoincrement", true),
                    Godina = table.Column<int>(type: "INTEGER", nullable: false),
                    Mesec = table.Column<int>(type: "INTEGER", nullable: false),
                    BrojRadnika = table.Column<int>(type: "INTEGER", nullable: true),
                    ImeRadnika = table.Column<string>(type: "TEXT", maxLength: 60, nullable: true),
                    Akcija = table.Column<int>(type: "INTEGER", nullable: false),
                    KorisnikId = table.Column<int>(type: "INTEGER", nullable: true),
                    KorisnickoIme = table.Column<string>(type: "TEXT", maxLength: 100, nullable: true),
                    Detalji = table.Column<string>(type: "TEXT", maxLength: 300, nullable: true),
                    Vreme = table.Column<DateTime>(type: "TEXT", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_ObracunAuditi", x => x.ObracunAuditId);
                });

            migrationBuilder.CreateTable(
                name: "PppPdPrijave",
                columns: table => new
                {
                    Id = table.Column<int>(type: "INTEGER", nullable: false)
                        .Annotation("Sqlite:Autoincrement", true),
                    Godina = table.Column<int>(type: "INTEGER", nullable: false),
                    Mesec = table.Column<int>(type: "INTEGER", nullable: false),
                    RedniBroj = table.Column<int>(type: "INTEGER", nullable: false),
                    VrstaPrijave = table.Column<string>(type: "TEXT", maxLength: 2, nullable: false),
                    KlijentskaOznaka = table.Column<string>(type: "TEXT", maxLength: 50, nullable: false),
                    DatumPlacanja = table.Column<DateTime>(type: "TEXT", nullable: false),
                    BrojZaposlenih = table.Column<int>(type: "INTEGER", nullable: false),
                    ZbirPoreza = table.Column<decimal>(type: "decimal(14,2)", nullable: false),
                    ZbirDoprinosa = table.Column<decimal>(type: "decimal(14,2)", nullable: false),
                    Bop = table.Column<string>(type: "TEXT", maxLength: 30, nullable: false),
                    Status = table.Column<int>(type: "INTEGER", nullable: false),
                    DatumPodnosenja = table.Column<DateTime>(type: "TEXT", nullable: true),
                    DatumStatusa = table.Column<DateTime>(type: "TEXT", nullable: true),
                    Napomena = table.Column<string>(type: "TEXT", maxLength: 500, nullable: false),
                    PutanjaFajla = table.Column<string>(type: "TEXT", maxLength: 260, nullable: false),
                    DatumKreiranja = table.Column<DateTime>(type: "TEXT", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_PppPdPrijave", x => x.Id);
                });

            migrationBuilder.CreateIndex(
                name: "IX_ObracunAuditi_Godina_Mesec_Vreme",
                table: "ObracunAuditi",
                columns: new[] { "Godina", "Mesec", "Vreme" });

            migrationBuilder.CreateIndex(
                name: "IX_PppPdPrijave_Godina_Mesec_RedniBroj",
                table: "PppPdPrijave",
                columns: new[] { "Godina", "Mesec", "RedniBroj" },
                unique: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "ObracunAuditi");

            migrationBuilder.DropTable(
                name: "PppPdPrijave");

            migrationBuilder.DropColumn(
                name: "Email",
                table: "Radnici");

            migrationBuilder.DropColumn(
                name: "OlaksicaVaziDo",
                table: "Radnici");

            migrationBuilder.DropColumn(
                name: "ProcenatPovracajaDoprinosa",
                table: "Radnici");

            migrationBuilder.DropColumn(
                name: "ProcenatPovracajaPoreza",
                table: "Radnici");

            migrationBuilder.DropColumn(
                name: "SifraMestaTroska",
                table: "Radnici");

            migrationBuilder.DropColumn(
                name: "ModelPozivaNaBroj",
                table: "Krediti");

            migrationBuilder.DropColumn(
                name: "PozivNaBroj",
                table: "Krediti");

            migrationBuilder.DropColumn(
                name: "PrimalacNaziv",
                table: "Krediti");

            migrationBuilder.DropColumn(
                name: "PrimalacRacun",
                table: "Krediti");

            migrationBuilder.DropColumn(
                name: "RedosledNaplate",
                table: "Krediti");

            migrationBuilder.DropColumn(
                name: "Tip",
                table: "Krediti");

            migrationBuilder.DropColumn(
                name: "SifraOpstine",
                table: "Firme");

            migrationBuilder.AddColumn<bool>(
                name: "Zakljucen",
                table: "ObracuniPlata",
                type: "INTEGER",
                nullable: false,
                defaultValue: false);
        }
    }
}
