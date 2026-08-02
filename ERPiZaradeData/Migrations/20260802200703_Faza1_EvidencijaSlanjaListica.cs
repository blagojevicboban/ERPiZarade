using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace ERPiZaradeData.Migrations
{
    /// <inheritdoc />
    public partial class Faza1_EvidencijaSlanjaListica : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "SlanjaListica",
                columns: table => new
                {
                    SlanjeListicaId = table.Column<int>(type: "INTEGER", nullable: false)
                        .Annotation("Sqlite:Autoincrement", true),
                    Godina = table.Column<int>(type: "INTEGER", nullable: false),
                    Mesec = table.Column<int>(type: "INTEGER", nullable: false),
                    BrojRadnika = table.Column<int>(type: "INTEGER", nullable: false),
                    ImeRadnika = table.Column<string>(type: "TEXT", maxLength: 60, nullable: false),
                    Email = table.Column<string>(type: "TEXT", maxLength: 120, nullable: false),
                    Ishod = table.Column<int>(type: "INTEGER", nullable: false),
                    ZasticenLozinkom = table.Column<bool>(type: "INTEGER", nullable: false),
                    Napomena = table.Column<string>(type: "TEXT", maxLength: 300, nullable: true),
                    KorisnikId = table.Column<int>(type: "INTEGER", nullable: true),
                    KorisnickoIme = table.Column<string>(type: "TEXT", maxLength: 100, nullable: true),
                    Vreme = table.Column<DateTime>(type: "TEXT", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_SlanjaListica", x => x.SlanjeListicaId);
                });

            migrationBuilder.CreateIndex(
                name: "IX_SlanjaListica_Godina_Mesec_BrojRadnika",
                table: "SlanjaListica",
                columns: new[] { "Godina", "Mesec", "BrojRadnika" });
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "SlanjaListica");
        }
    }
}
