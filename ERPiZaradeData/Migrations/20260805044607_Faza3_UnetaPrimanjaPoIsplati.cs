using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace ERPiZaradeData.Migrations
{
    /// <summary>
    /// Faza 3.2 — uneta primanja se vode po isplati, ne po mesecu (isti obrazac kao
    /// Faza2_RadniSatiPoIsplati za radne sate).
    ///
    /// <c>UnetoPrimanje</c> je bilo jedinstveno po (radnik, period, vrsta), pa je isti unos
    /// ulazio i u akontaciju i u konačnu zaradu istog meseca — dvaput obračunat. Migracija
    /// dodaje kolonu, proširuje jedinstveni indeks i upisuje prvu isplatu perioda u zatečene
    /// redove (isti razlog kao kod radnih sati: SQLite NULL-ove u jedinstvenom indeksu smatra
    /// međusobno različitim, pa bi dva unosa za istog radnika prošla nezapaženo). Nijedan iznos
    /// se ne menja.
    ///
    /// Uz to dodaje <c>VrstaPrimanja.VecIsplacenoVanObracuna</c> — obeležje za primanja koja je
    /// radnik već primio van platnog spiska (npr. prekoračenje dnevnice iz putnog naloga u
    /// ERPiFinansije). Podrazumevano netačno, pa se nijedna zatečena vrsta ne menja.
    /// </summary>
    public partial class Faza3_UnetaPrimanjaPoIsplati : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "IX_UnetaPrimanja_RadnikId_Godina_Mesec_VrstaPrimanjaId",
                table: "UnetaPrimanja");

            migrationBuilder.AddColumn<bool>(
                name: "VecIsplacenoVanObracuna",
                table: "VrstePrimanja",
                type: "INTEGER",
                nullable: false,
                defaultValue: false);

            migrationBuilder.AddColumn<int>(
                name: "IsplataId",
                table: "UnetaPrimanja",
                type: "INTEGER",
                nullable: true);

            migrationBuilder.CreateIndex(
                name: "IX_UnetaPrimanja_IsplataId",
                table: "UnetaPrimanja",
                column: "IsplataId");

            migrationBuilder.CreateIndex(
                name: "IX_UnetaPrimanja_RadnikId_Godina_Mesec_VrstaPrimanjaId_IsplataId",
                table: "UnetaPrimanja",
                columns: new[] { "RadnikId", "Godina", "Mesec", "VrstaPrimanjaId", "IsplataId" },
                unique: true);

            migrationBuilder.AddForeignKey(
                name: "FK_UnetaPrimanja_Isplate_IsplataId",
                table: "UnetaPrimanja",
                column: "IsplataId",
                principalTable: "Isplate",
                principalColumn: "IsplataId",
                onDelete: ReferentialAction.Restrict);

            // Period koji ima uneta primanja, a nema isplatu, dobija prvu — isto što je
            // Faza2_RadniSatiPoIsplati uradila za radne sate. Primanje može postojati i bez
            // isplate: unese se, pa se obračun tek pokrene.
            migrationBuilder.Sql(@"
                INSERT INTO Isplate (Godina, Mesec, RedniBroj, Vrsta, Opis, DatumIsplate, DatumKreiranja)
                SELECT DISTINCT p.Godina, p.Mesec, 1, 0, '',
                       datetime(printf('%04d-%02d-01', p.Godina, p.Mesec), '+1 month', '-1 day'),
                       datetime('now', 'localtime')
                FROM UnetaPrimanja p
                WHERE p.Godina > 0 AND p.Mesec BETWEEN 1 AND 12
                  AND NOT EXISTS (
                      SELECT 1 FROM Isplate i
                      WHERE i.Godina = p.Godina AND i.Mesec = p.Mesec AND i.RedniBroj = 1);");

            // Zatečeni redovi se vezuju za prvu isplatu svog perioda, iz istog razloga kao kod
            // radnih sati — bez ovoga bi jedinstveni indeks nad njima bio bezuban.
            migrationBuilder.Sql(@"
                UPDATE UnetaPrimanja
                SET IsplataId = (
                    SELECT i.IsplataId FROM Isplate i
                    WHERE i.Godina = UnetaPrimanja.Godina
                      AND i.Mesec = UnetaPrimanja.Mesec
                      AND i.RedniBroj = 1)
                WHERE IsplataId IS NULL;");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_UnetaPrimanja_Isplate_IsplataId",
                table: "UnetaPrimanja");

            migrationBuilder.DropIndex(
                name: "IX_UnetaPrimanja_IsplataId",
                table: "UnetaPrimanja");

            migrationBuilder.DropIndex(
                name: "IX_UnetaPrimanja_RadnikId_Godina_Mesec_VrstaPrimanjaId_IsplataId",
                table: "UnetaPrimanja");

            migrationBuilder.DropColumn(
                name: "VecIsplacenoVanObracuna",
                table: "VrstePrimanja");

            migrationBuilder.DropColumn(
                name: "IsplataId",
                table: "UnetaPrimanja");

            migrationBuilder.CreateIndex(
                name: "IX_UnetaPrimanja_RadnikId_Godina_Mesec_VrstaPrimanjaId",
                table: "UnetaPrimanja",
                columns: new[] { "RadnikId", "Godina", "Mesec", "VrstaPrimanjaId" },
                unique: true);
        }
    }
}
