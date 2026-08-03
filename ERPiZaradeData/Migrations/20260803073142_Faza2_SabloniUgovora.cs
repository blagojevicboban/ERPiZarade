using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace ERPiZaradeData.Migrations
{
    /// <inheritdoc />
    public partial class Faza2_SabloniUgovora : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<DateTime>(
                name: "DatumTeksta",
                table: "Ugovori",
                type: "TEXT",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "Tekst",
                table: "Ugovori",
                type: "TEXT",
                nullable: false,
                defaultValue: "");

            migrationBuilder.AddColumn<string>(
                name: "FunkcijaZastupnika",
                table: "Firme",
                type: "TEXT",
                maxLength: 40,
                nullable: false,
                defaultValue: "");

            migrationBuilder.AddColumn<string>(
                name: "Zastupnik",
                table: "Firme",
                type: "TEXT",
                maxLength: 60,
                nullable: false,
                defaultValue: "");

            migrationBuilder.CreateTable(
                name: "SabloniUgovora",
                columns: table => new
                {
                    SablonUgovoraId = table.Column<int>(type: "INTEGER", nullable: false)
                        .Annotation("Sqlite:Autoincrement", true),
                    Sifra = table.Column<string>(type: "TEXT", maxLength: 10, nullable: false),
                    Naziv = table.Column<string>(type: "TEXT", maxLength: 80, nullable: false),
                    VrstaUgovoraId = table.Column<int>(type: "INTEGER", nullable: true),
                    Tekst = table.Column<string>(type: "TEXT", nullable: false),
                    Redosled = table.Column<int>(type: "INTEGER", nullable: false),
                    Aktivan = table.Column<bool>(type: "INTEGER", nullable: false),
                    JeSistemski = table.Column<bool>(type: "INTEGER", nullable: false),
                    Napomena = table.Column<string>(type: "TEXT", maxLength: 200, nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_SabloniUgovora", x => x.SablonUgovoraId);
                    table.ForeignKey(
                        name: "FK_SabloniUgovora_VrsteUgovora_VrstaUgovoraId",
                        column: x => x.VrstaUgovoraId,
                        principalTable: "VrsteUgovora",
                        principalColumn: "VrstaUgovoraId",
                        onDelete: ReferentialAction.SetNull);
                });

            migrationBuilder.CreateIndex(
                name: "IX_SabloniUgovora_Sifra",
                table: "SabloniUgovora",
                column: "Sifra",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_SabloniUgovora_VrstaUgovoraId",
                table: "SabloniUgovora",
                column: "VrstaUgovoraId");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "SabloniUgovora");

            migrationBuilder.DropColumn(
                name: "DatumTeksta",
                table: "Ugovori");

            migrationBuilder.DropColumn(
                name: "Tekst",
                table: "Ugovori");

            migrationBuilder.DropColumn(
                name: "FunkcijaZastupnika",
                table: "Firme");

            migrationBuilder.DropColumn(
                name: "Zastupnik",
                table: "Firme");
        }
    }
}
