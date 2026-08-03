using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace ERPiZaradeData.Migrations
{
    /// <summary>
    /// Dovršetak Faze 2.2 — radni sati se vode po isplati, ne po mesecu.
    ///
    /// <c>RadniSat</c> je bio jedinstven po (radnik, godina, mesec), pa je unos sati za drugu
    /// isplatu meseca prepisivao onaj za prvu. Iznosi već napravljenih obračuna time nisu bili
    /// ugroženi — svaki obračun nosi svoje sate u svojim kolonama — ali je ekran radnih sati
    /// pokazivao poslednji unos, ma za koju isplatu bio rađen.
    ///
    /// Migracija je čisto dodavanje: nova kolona, prošireni jedinstveni indeks i upis prve
    /// isplate perioda u zatečene redove. Nijedan sat se ne menja.
    /// </summary>
    public partial class Faza2_RadniSatiPoIsplati : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "IX_RadniSati_RadnikId_Godina_Mesec",
                table: "RadniSati");

            migrationBuilder.AddColumn<int>(
                name: "IsplataId",
                table: "RadniSati",
                type: "INTEGER",
                nullable: true);

            migrationBuilder.CreateIndex(
                name: "IX_RadniSati_IsplataId",
                table: "RadniSati",
                column: "IsplataId");

            migrationBuilder.CreateIndex(
                name: "IX_RadniSati_RadnikId_Godina_Mesec_IsplataId",
                table: "RadniSati",
                columns: new[] { "RadnikId", "Godina", "Mesec", "IsplataId" },
                unique: true);

            migrationBuilder.AddForeignKey(
                name: "FK_RadniSati_Isplate_IsplataId",
                table: "RadniSati",
                column: "IsplataId",
                principalTable: "Isplate",
                principalColumn: "IsplataId",
                onDelete: ReferentialAction.Restrict);

            // Period koji ima unete sate, a nema isplatu, dobija prvu — isto kao što je
            // Faza2_Isplate uradila za periode sa obračunima. Sati mogu postojati i bez
            // obračuna: unesu se, pa se obračun tek pokrene.
            migrationBuilder.Sql(@"
                INSERT INTO Isplate (Godina, Mesec, RedniBroj, Vrsta, Opis, DatumIsplate, DatumKreiranja)
                SELECT DISTINCT s.Godina, s.Mesec, 1, 0, '',
                       datetime(printf('%04d-%02d-01', s.Godina, s.Mesec), '+1 month', '-1 day'),
                       datetime('now', 'localtime')
                FROM RadniSati s
                WHERE s.Godina > 0 AND s.Mesec BETWEEN 1 AND 12
                  AND NOT EXISTS (
                      SELECT 1 FROM Isplate i
                      WHERE i.Godina = s.Godina AND i.Mesec = s.Mesec AND i.RedniBroj = 1);");

            // Zatečeni redovi se vezuju za prvu isplatu svog perioda. Bez ovoga bi ostali sa
            // NULL, što obuhvat i dalje razume, ali bi jedinstveni indeks nad njima bio
            // bezuban: SQLite NULL-ove u jedinstvenom indeksu smatra međusobno različitim,
            // pa bi dva unosa za istog radnika u istom mesecu prošla.
            migrationBuilder.Sql(@"
                UPDATE RadniSati
                SET IsplataId = (
                    SELECT i.IsplataId FROM Isplate i
                    WHERE i.Godina = RadniSati.Godina
                      AND i.Mesec = RadniSati.Mesec
                      AND i.RedniBroj = 1)
                WHERE IsplataId IS NULL;");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_RadniSati_Isplate_IsplataId",
                table: "RadniSati");

            migrationBuilder.DropIndex(
                name: "IX_RadniSati_IsplataId",
                table: "RadniSati");

            migrationBuilder.DropIndex(
                name: "IX_RadniSati_RadnikId_Godina_Mesec_IsplataId",
                table: "RadniSati");

            migrationBuilder.DropColumn(
                name: "IsplataId",
                table: "RadniSati");

            migrationBuilder.CreateIndex(
                name: "IX_RadniSati_RadnikId_Godina_Mesec",
                table: "RadniSati",
                columns: new[] { "RadnikId", "Godina", "Mesec" },
                unique: true);
        }
    }
}
