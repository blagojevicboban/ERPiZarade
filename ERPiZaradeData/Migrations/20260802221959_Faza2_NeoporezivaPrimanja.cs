using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace ERPiZaradeData.Migrations
{
    /// <inheritdoc />
    public partial class Faza2_NeoporezivaPrimanja : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<decimal>(
                name: "OporeziviDeo",
                table: "ObracunStavke",
                type: "decimal(14,2)",
                nullable: false,
                defaultValue: 0m);

            migrationBuilder.CreateTable(
                name: "UnetaPrimanja",
                columns: table => new
                {
                    UnetoPrimanjeId = table.Column<int>(type: "INTEGER", nullable: false)
                        .Annotation("Sqlite:Autoincrement", true),
                    RadnikId = table.Column<int>(type: "INTEGER", nullable: false),
                    Godina = table.Column<int>(type: "INTEGER", nullable: false),
                    Mesec = table.Column<int>(type: "INTEGER", nullable: false),
                    VrstaPrimanjaId = table.Column<int>(type: "INTEGER", nullable: false),
                    Iznos = table.Column<decimal>(type: "decimal(14,2)", nullable: false),
                    Napomena = table.Column<string>(type: "TEXT", maxLength: 200, nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_UnetaPrimanja", x => x.UnetoPrimanjeId);
                    table.ForeignKey(
                        name: "FK_UnetaPrimanja_Radnici_RadnikId",
                        column: x => x.RadnikId,
                        principalTable: "Radnici",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_UnetaPrimanja_VrstePrimanja_VrstaPrimanjaId",
                        column: x => x.VrstaPrimanjaId,
                        principalTable: "VrstePrimanja",
                        principalColumn: "VrstaPrimanjaId",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateIndex(
                name: "IX_UnetaPrimanja_RadnikId_Godina_Mesec_VrstaPrimanjaId",
                table: "UnetaPrimanja",
                columns: new[] { "RadnikId", "Godina", "Mesec", "VrstaPrimanjaId" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_UnetaPrimanja_VrstaPrimanjaId",
                table: "UnetaPrimanja",
                column: "VrstaPrimanjaId");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "UnetaPrimanja");

            migrationBuilder.DropColumn(
                name: "OporeziviDeo",
                table: "ObracunStavke");
        }
    }
}
