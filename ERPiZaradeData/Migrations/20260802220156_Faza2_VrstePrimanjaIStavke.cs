using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace ERPiZaradeData.Migrations
{
    /// <inheritdoc />
    public partial class Faza2_VrstePrimanjaIStavke : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "VrstePrimanja",
                columns: table => new
                {
                    VrstaPrimanjaId = table.Column<int>(type: "INTEGER", nullable: false)
                        .Annotation("Sqlite:Autoincrement", true),
                    Sifra = table.Column<string>(type: "TEXT", maxLength: 10, nullable: false),
                    Naziv = table.Column<string>(type: "TEXT", maxLength: 80, nullable: false),
                    Svp = table.Column<string>(type: "TEXT", maxLength: 9, nullable: false),
                    Oporezivo = table.Column<bool>(type: "INTEGER", nullable: false),
                    UlaziUOsnovicuDoprinosa = table.Column<bool>(type: "INTEGER", nullable: false),
                    NeoporeziviLimit = table.Column<decimal>(type: "decimal(14,2)", nullable: false),
                    Konto = table.Column<string>(type: "TEXT", maxLength: 10, nullable: false),
                    Redosled = table.Column<int>(type: "INTEGER", nullable: false),
                    Aktivna = table.Column<bool>(type: "INTEGER", nullable: false),
                    JeSistemska = table.Column<bool>(type: "INTEGER", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_VrstePrimanja", x => x.VrstaPrimanjaId);
                });

            migrationBuilder.CreateTable(
                name: "ObracunStavke",
                columns: table => new
                {
                    ObracunStavkaId = table.Column<int>(type: "INTEGER", nullable: false)
                        .Annotation("Sqlite:Autoincrement", true),
                    ObracunPlateId = table.Column<int>(type: "INTEGER", nullable: false),
                    VrstaPrimanjaId = table.Column<int>(type: "INTEGER", nullable: false),
                    Sati = table.Column<int>(type: "INTEGER", nullable: false),
                    Iznos = table.Column<decimal>(type: "decimal(14,2)", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_ObracunStavke", x => x.ObracunStavkaId);
                    table.ForeignKey(
                        name: "FK_ObracunStavke_ObracuniPlata_ObracunPlateId",
                        column: x => x.ObracunPlateId,
                        principalTable: "ObracuniPlata",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_ObracunStavke_VrstePrimanja_VrstaPrimanjaId",
                        column: x => x.VrstaPrimanjaId,
                        principalTable: "VrstePrimanja",
                        principalColumn: "VrstaPrimanjaId",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateIndex(
                name: "IX_ObracunStavke_ObracunPlateId_VrstaPrimanjaId",
                table: "ObracunStavke",
                columns: new[] { "ObracunPlateId", "VrstaPrimanjaId" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_ObracunStavke_VrstaPrimanjaId",
                table: "ObracunStavke",
                column: "VrstaPrimanjaId");

            migrationBuilder.CreateIndex(
                name: "IX_VrstePrimanja_Sifra",
                table: "VrstePrimanja",
                column: "Sifra",
                unique: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "ObracunStavke");

            migrationBuilder.DropTable(
                name: "VrstePrimanja");
        }
    }
}
