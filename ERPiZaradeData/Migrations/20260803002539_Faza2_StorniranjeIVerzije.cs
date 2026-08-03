using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace ERPiZaradeData.Migrations
{
    /// <inheritdoc />
    public partial class Faza2_StorniranjeIVerzije : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "BrojResenja",
                table: "PppPdPrijave",
                type: "TEXT",
                maxLength: 200,
                nullable: false,
                defaultValue: "");

            migrationBuilder.AddColumn<string>(
                name: "Jipd",
                table: "PppPdPrijave",
                type: "TEXT",
                maxLength: 20,
                nullable: false,
                defaultValue: "");

            migrationBuilder.AddColumn<string>(
                name: "JipdKojiSeMenja",
                table: "PppPdPrijave",
                type: "TEXT",
                maxLength: 20,
                nullable: false,
                defaultValue: "");

            migrationBuilder.AddColumn<int>(
                name: "OsnovIzmene",
                table: "PppPdPrijave",
                type: "INTEGER",
                nullable: false,
                defaultValue: 0);

            migrationBuilder.AddColumn<int>(
                name: "VrstaIzmene",
                table: "PppPdPrijave",
                type: "INTEGER",
                nullable: false,
                defaultValue: 0);

            migrationBuilder.AddColumn<DateTime>(
                name: "DatumStorniranja",
                table: "ObracuniPlata",
                type: "TEXT",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "RazlogStorniranja",
                table: "ObracuniPlata",
                type: "TEXT",
                maxLength: 200,
                nullable: false,
                defaultValue: "");

            migrationBuilder.AddColumn<bool>(
                name: "Storniran",
                table: "ObracuniPlata",
                type: "INTEGER",
                nullable: false,
                defaultValue: false);

            migrationBuilder.AddColumn<int>(
                name: "Verzija",
                table: "ObracuniPlata",
                type: "INTEGER",
                nullable: false,
                defaultValue: 0);

            // Zatečeni obračuni su prva verzija, ne nulta — inače bi prva prekalkulacija
            // arhivirala „verziju 0" i sledeći obračun bi dobio broj koji je već potrošen.
            migrationBuilder.Sql("UPDATE ObracuniPlata SET Verzija = 1;");

            migrationBuilder.CreateTable(
                name: "ObracunVerzije",
                columns: table => new
                {
                    ObracunVerzijaId = table.Column<int>(type: "INTEGER", nullable: false)
                        .Annotation("Sqlite:Autoincrement", true),
                    Godina = table.Column<int>(type: "INTEGER", nullable: false),
                    Mesec = table.Column<int>(type: "INTEGER", nullable: false),
                    RadnikId = table.Column<int>(type: "INTEGER", nullable: false),
                    BrojRadnika = table.Column<int>(type: "INTEGER", nullable: false),
                    ImeRadnika = table.Column<string>(type: "TEXT", maxLength: 60, nullable: false),
                    Verzija = table.Column<int>(type: "INTEGER", nullable: false),
                    Razlog = table.Column<string>(type: "TEXT", maxLength: 300, nullable: false),
                    KorisnickoIme = table.Column<string>(type: "TEXT", maxLength: 100, nullable: true),
                    Vreme = table.Column<DateTime>(type: "TEXT", nullable: false),
                    BioZakljucan = table.Column<bool>(type: "INTEGER", nullable: false),
                    BioStorniran = table.Column<bool>(type: "INTEGER", nullable: false),
                    Bruto = table.Column<decimal>(type: "decimal(14,2)", nullable: false),
                    PorezNaDohodak = table.Column<decimal>(type: "decimal(14,2)", nullable: false),
                    DoprinosiRadnik = table.Column<decimal>(type: "decimal(14,2)", nullable: false),
                    DoprinosiPoslodavac = table.Column<decimal>(type: "decimal(14,2)", nullable: false),
                    NetoIsplata = table.Column<decimal>(type: "decimal(14,2)", nullable: false),
                    Snimak = table.Column<string>(type: "TEXT", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_ObracunVerzije", x => x.ObracunVerzijaId);
                });

            migrationBuilder.CreateIndex(
                name: "IX_ObracunVerzije_Godina_Mesec_BrojRadnika_Verzija",
                table: "ObracunVerzije",
                columns: new[] { "Godina", "Mesec", "BrojRadnika", "Verzija" });
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "ObracunVerzije");

            migrationBuilder.DropColumn(
                name: "BrojResenja",
                table: "PppPdPrijave");

            migrationBuilder.DropColumn(
                name: "Jipd",
                table: "PppPdPrijave");

            migrationBuilder.DropColumn(
                name: "JipdKojiSeMenja",
                table: "PppPdPrijave");

            migrationBuilder.DropColumn(
                name: "OsnovIzmene",
                table: "PppPdPrijave");

            migrationBuilder.DropColumn(
                name: "VrstaIzmene",
                table: "PppPdPrijave");

            migrationBuilder.DropColumn(
                name: "DatumStorniranja",
                table: "ObracuniPlata");

            migrationBuilder.DropColumn(
                name: "RazlogStorniranja",
                table: "ObracuniPlata");

            migrationBuilder.DropColumn(
                name: "Storniran",
                table: "ObracuniPlata");

            migrationBuilder.DropColumn(
                name: "Verzija",
                table: "ObracuniPlata");
        }
    }
}
