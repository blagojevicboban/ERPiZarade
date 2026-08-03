using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace ERPiZaradeData.Migrations
{
    /// <inheritdoc />
    public partial class Faza2_UgovoriVanRadnogOdnosa : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<bool>(
                name: "VanRadnogOdnosa",
                table: "Radnici",
                type: "INTEGER",
                nullable: false,
                defaultValue: false);

            migrationBuilder.AddColumn<decimal>(
                name: "OsnovicaDoprinosa",
                table: "ObracuniPlata",
                type: "decimal(14,2)",
                nullable: true);

            migrationBuilder.AddColumn<int>(
                name: "UgovorId",
                table: "ObracuniPlata",
                type: "INTEGER",
                nullable: true);

            migrationBuilder.CreateTable(
                name: "VrsteUgovora",
                columns: table => new
                {
                    VrstaUgovoraId = table.Column<int>(type: "INTEGER", nullable: false)
                        .Annotation("Sqlite:Autoincrement", true),
                    Sifra = table.Column<string>(type: "TEXT", maxLength: 10, nullable: false),
                    Naziv = table.Column<string>(type: "TEXT", maxLength: 80, nullable: false),
                    Ovp = table.Column<string>(type: "TEXT", maxLength: 3, nullable: false),
                    NormiraniTroskoviProcenat = table.Column<decimal>(type: "decimal(6,2)", nullable: false),
                    StopaPoreza = table.Column<decimal>(type: "decimal(6,2)", nullable: false),
                    StopaPioPrimalac = table.Column<decimal>(type: "decimal(6,2)", nullable: false),
                    StopaZdravstvoPrimalac = table.Column<decimal>(type: "decimal(6,2)", nullable: false),
                    StopaNezaposlenostPrimalac = table.Column<decimal>(type: "decimal(6,2)", nullable: false),
                    StopaPioIsplatilac = table.Column<decimal>(type: "decimal(6,2)", nullable: false),
                    StopaZdravstvoIsplatilac = table.Column<decimal>(type: "decimal(6,2)", nullable: false),
                    StopaNezaposlenostIsplatilac = table.Column<decimal>(type: "decimal(6,2)", nullable: false),
                    Konto = table.Column<string>(type: "TEXT", maxLength: 10, nullable: false),
                    SifraPlacanja = table.Column<string>(type: "TEXT", maxLength: 3, nullable: false),
                    Redosled = table.Column<int>(type: "INTEGER", nullable: false),
                    Aktivna = table.Column<bool>(type: "INTEGER", nullable: false),
                    Napomena = table.Column<string>(type: "TEXT", maxLength: 200, nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_VrsteUgovora", x => x.VrstaUgovoraId);
                });

            migrationBuilder.CreateTable(
                name: "Ugovori",
                columns: table => new
                {
                    UgovorId = table.Column<int>(type: "INTEGER", nullable: false)
                        .Annotation("Sqlite:Autoincrement", true),
                    VrstaUgovoraId = table.Column<int>(type: "INTEGER", nullable: false),
                    BrojRadnika = table.Column<int>(type: "INTEGER", nullable: false),
                    TipPrimaoca = table.Column<int>(type: "INTEGER", nullable: false),
                    Broj = table.Column<string>(type: "TEXT", maxLength: 20, nullable: false),
                    Predmet = table.Column<string>(type: "TEXT", maxLength: 200, nullable: false),
                    DatumZakljucenja = table.Column<DateTime>(type: "TEXT", nullable: false),
                    DatumOd = table.Column<DateTime>(type: "TEXT", nullable: true),
                    DatumDo = table.Column<DateTime>(type: "TEXT", nullable: true),
                    UgovorenIznos = table.Column<decimal>(type: "decimal(14,2)", nullable: false),
                    IznosJeNeto = table.Column<bool>(type: "INTEGER", nullable: false),
                    Aktivan = table.Column<bool>(type: "INTEGER", nullable: false),
                    Napomena = table.Column<string>(type: "TEXT", maxLength: 200, nullable: false),
                    DatumUnosa = table.Column<DateTime>(type: "TEXT", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Ugovori", x => x.UgovorId);
                    table.ForeignKey(
                        name: "FK_Ugovori_VrsteUgovora_VrstaUgovoraId",
                        column: x => x.VrstaUgovoraId,
                        principalTable: "VrsteUgovora",
                        principalColumn: "VrstaUgovoraId",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateIndex(
                name: "IX_ObracuniPlata_UgovorId",
                table: "ObracuniPlata",
                column: "UgovorId");

            migrationBuilder.CreateIndex(
                name: "IX_Ugovori_BrojRadnika",
                table: "Ugovori",
                column: "BrojRadnika");

            migrationBuilder.CreateIndex(
                name: "IX_Ugovori_VrstaUgovoraId",
                table: "Ugovori",
                column: "VrstaUgovoraId");

            migrationBuilder.CreateIndex(
                name: "IX_VrsteUgovora_Sifra",
                table: "VrsteUgovora",
                column: "Sifra",
                unique: true);

            migrationBuilder.AddForeignKey(
                name: "FK_ObracuniPlata_Ugovori_UgovorId",
                table: "ObracuniPlata",
                column: "UgovorId",
                principalTable: "Ugovori",
                principalColumn: "UgovorId",
                onDelete: ReferentialAction.Restrict);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_ObracuniPlata_Ugovori_UgovorId",
                table: "ObracuniPlata");

            migrationBuilder.DropTable(
                name: "Ugovori");

            migrationBuilder.DropTable(
                name: "VrsteUgovora");

            migrationBuilder.DropIndex(
                name: "IX_ObracuniPlata_UgovorId",
                table: "ObracuniPlata");

            migrationBuilder.DropColumn(
                name: "VanRadnogOdnosa",
                table: "Radnici");

            migrationBuilder.DropColumn(
                name: "OsnovicaDoprinosa",
                table: "ObracuniPlata");

            migrationBuilder.DropColumn(
                name: "UgovorId",
                table: "ObracuniPlata");
        }
    }
}
