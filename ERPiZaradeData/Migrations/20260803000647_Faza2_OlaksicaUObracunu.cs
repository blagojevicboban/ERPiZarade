using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace ERPiZaradeData.Migrations
{
    /// <inheritdoc />
    public partial class Faza2_OlaksicaUObracunu : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<decimal>(
                name: "OlaksicaDoprinosi",
                table: "ObracuniPlata",
                type: "decimal(14,2)",
                nullable: false,
                defaultValue: 0m);

            migrationBuilder.AddColumn<string>(
                name: "OlaksicaOznaka",
                table: "ObracuniPlata",
                type: "TEXT",
                maxLength: 2,
                nullable: false,
                defaultValue: "");

            migrationBuilder.AddColumn<decimal>(
                name: "OlaksicaPorez",
                table: "ObracuniPlata",
                type: "decimal(14,2)",
                nullable: false,
                defaultValue: 0m);

            migrationBuilder.AddColumn<bool>(
                name: "OlaksicaUmanjujeUplatu",
                table: "ObracuniPlata",
                type: "INTEGER",
                nullable: false,
                defaultValue: false);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "OlaksicaDoprinosi",
                table: "ObracuniPlata");

            migrationBuilder.DropColumn(
                name: "OlaksicaOznaka",
                table: "ObracuniPlata");

            migrationBuilder.DropColumn(
                name: "OlaksicaPorez",
                table: "ObracuniPlata");

            migrationBuilder.DropColumn(
                name: "OlaksicaUmanjujeUplatu",
                table: "ObracuniPlata");
        }
    }
}
