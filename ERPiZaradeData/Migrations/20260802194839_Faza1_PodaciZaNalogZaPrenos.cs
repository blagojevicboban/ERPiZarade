using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace ERPiZaradeData.Migrations
{
    /// <inheritdoc />
    public partial class Faza1_PodaciZaNalogZaPrenos : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<decimal>(
                name: "IznosZaUplatu",
                table: "PppPdPrijave",
                type: "decimal(14,2)",
                nullable: false,
                defaultValue: 0m);

            migrationBuilder.AddColumn<string>(
                name: "ModelPozivaNaBroj",
                table: "PppPdPrijave",
                type: "TEXT",
                maxLength: 2,
                nullable: false,
                defaultValue: "");

            migrationBuilder.AddColumn<string>(
                name: "RacunZaUplatu",
                table: "PppPdPrijave",
                type: "TEXT",
                maxLength: 25,
                nullable: false,
                defaultValue: "");

            migrationBuilder.AddColumn<string>(
                name: "SvrhaUplate",
                table: "PppPdPrijave",
                type: "TEXT",
                maxLength: 140,
                nullable: false,
                defaultValue: "");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "IznosZaUplatu",
                table: "PppPdPrijave");

            migrationBuilder.DropColumn(
                name: "ModelPozivaNaBroj",
                table: "PppPdPrijave");

            migrationBuilder.DropColumn(
                name: "RacunZaUplatu",
                table: "PppPdPrijave");

            migrationBuilder.DropColumn(
                name: "SvrhaUplate",
                table: "PppPdPrijave");
        }
    }
}
