using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace ERPiZaradeData.Migrations
{
    /// <inheritdoc />
    public partial class Faza2_BolovanjaIRfzoObrasci : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<bool>(
                name: "NaTeretFonda",
                table: "VrstePrimanja",
                type: "INTEGER",
                nullable: false,
                defaultValue: false);

            migrationBuilder.AddColumn<string>(
                name: "Lbo",
                table: "Radnici",
                type: "TEXT",
                maxLength: 11,
                nullable: false,
                defaultValue: "");

            migrationBuilder.AddColumn<string>(
                name: "PodracunPoslovneJedinice",
                table: "Firme",
                type: "TEXT",
                maxLength: 30,
                nullable: false,
                defaultValue: "");

            migrationBuilder.AddColumn<string>(
                name: "PosebanRacun",
                table: "Firme",
                type: "TEXT",
                maxLength: 30,
                nullable: false,
                defaultValue: "");

            migrationBuilder.AddColumn<string>(
                name: "SifraDelatnosti",
                table: "Firme",
                type: "TEXT",
                maxLength: 10,
                nullable: false,
                defaultValue: "");

            migrationBuilder.CreateTable(
                name: "Bolovanja",
                columns: table => new
                {
                    BolovanjeId = table.Column<int>(type: "INTEGER", nullable: false)
                        .Annotation("Sqlite:Autoincrement", true),
                    BrojRadnika = table.Column<int>(type: "INTEGER", nullable: false),
                    Godina = table.Column<int>(type: "INTEGER", nullable: false),
                    Mesec = table.Column<int>(type: "INTEGER", nullable: false),
                    DatumPocetkaSprecenosti = table.Column<DateTime>(type: "TEXT", nullable: false),
                    DatumOd = table.Column<DateTime>(type: "TEXT", nullable: false),
                    DatumDo = table.Column<DateTime>(type: "TEXT", nullable: false),
                    Osnov = table.Column<int>(type: "INTEGER", nullable: false),
                    PrvaIsplata = table.Column<bool>(type: "INTEGER", nullable: false),
                    BrojDoznake = table.Column<string>(type: "TEXT", maxLength: 30, nullable: false),
                    Napomena = table.Column<string>(type: "TEXT", maxLength: 200, nullable: false),
                    DatumUnosa = table.Column<DateTime>(type: "TEXT", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Bolovanja", x => x.BolovanjeId);
                });

            migrationBuilder.CreateIndex(
                name: "IX_Bolovanja_BrojRadnika_Godina_Mesec_DatumOd",
                table: "Bolovanja",
                columns: new[] { "BrojRadnika", "Godina", "Mesec", "DatumOd" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_Bolovanja_Godina_Mesec_BrojRadnika",
                table: "Bolovanja",
                columns: new[] { "Godina", "Mesec", "BrojRadnika" });

            // „Bolovanje preko 30 dana" je jedina vrsta koja po Zakonu o zdravstvenom
            // osiguranju sigurno ide na teret Fonda, pa se u zatečenim bazama označava ovde —
            // dopuna šifarnika pri pokretanju dodaje samo vrste kojih nema, a ova postoji od
            // Faze 2.1. Ostale (povreda na radu, nega člana porodice) korisnik označava sam,
            // jer zavise od toga šta filijala refundira.
            //
            // Polje do ove verzije nije postojalo, pa se ovim ne menja nijedan zatečen
            // obračun ni izveštaj — prvi put se čita u obrascu OZ-10.
            migrationBuilder.Sql(
                "UPDATE VrstePrimanja SET NaTeretFonda = 1 WHERE Sifra = 'B60';");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "Bolovanja");

            migrationBuilder.DropColumn(
                name: "NaTeretFonda",
                table: "VrstePrimanja");

            migrationBuilder.DropColumn(
                name: "Lbo",
                table: "Radnici");

            migrationBuilder.DropColumn(
                name: "PodracunPoslovneJedinice",
                table: "Firme");

            migrationBuilder.DropColumn(
                name: "PosebanRacun",
                table: "Firme");

            migrationBuilder.DropColumn(
                name: "SifraDelatnosti",
                table: "Firme");
        }
    }
}
