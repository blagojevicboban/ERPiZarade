using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace ERPiZaradeData.Migrations
{
    /// <inheritdoc />
    public partial class Faza3_KontaKnjizenja : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "KontaKnjizenja",
                columns: table => new
                {
                    KontoKnjizenjaId = table.Column<int>(type: "INTEGER", nullable: false)
                        .Annotation("Sqlite:Autoincrement", true),
                    Kljuc = table.Column<string>(type: "TEXT", maxLength: 40, nullable: false),
                    Naziv = table.Column<string>(type: "TEXT", maxLength: 120, nullable: false),
                    Konto = table.Column<string>(type: "TEXT", maxLength: 20, nullable: false),
                    Strana = table.Column<int>(type: "INTEGER", nullable: false),
                    Redosled = table.Column<int>(type: "INTEGER", nullable: false),
                    Napomena = table.Column<string>(type: "TEXT", maxLength: 250, nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_KontaKnjizenja", x => x.KontoKnjizenjaId);
                });

            migrationBuilder.CreateIndex(
                name: "IX_KontaKnjizenja_Kljuc",
                table: "KontaKnjizenja",
                column: "Kljuc",
                unique: true);

            // Ispravka podrazumevanog konta naknada zarade. Do sada je sistemskim vrstama
            // „godišnji odmor", „praznik", „bolovanje" i sličnima upisivan konto 521, koji
            // po Kontnom okviru nosi samo doprinose na teret poslodavca; naknada zarade ide
            // na 520 („Troškovi zarada i naknada zarada (bruto)"), zajedno sa zaradom.
            //
            // Polje do ove verzije nije koristio niko — uvedeno je za Fazu 3.1 i prvi put se
            // čita ovde — pa se ispravkom ne menja nijedan zatečen izveštaj ni obračun.
            // Dira samo sistemske vrste i samo ako je vrednost ostala nepromenjena; konto
            // koji je korisnik prilagodio svom kontnom planu ostaje netaknut.
            migrationBuilder.Sql(
                "UPDATE VrstePrimanja SET Konto = '520' WHERE JeSistemska = 1 AND Konto = '521';");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "KontaKnjizenja");
        }
    }
}
