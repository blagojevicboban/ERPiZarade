using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace ERPiZaradeData.Migrations
{
    /// <inheritdoc />
    public partial class Faza1_KalendarPraznika : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "Praznici",
                columns: table => new
                {
                    PraznikId = table.Column<int>(type: "INTEGER", nullable: false)
                        .Annotation("Sqlite:Autoincrement", true),
                    Datum = table.Column<DateTime>(type: "TEXT", nullable: false),
                    Naziv = table.Column<string>(type: "TEXT", maxLength: 80, nullable: false),
                    Neradni = table.Column<bool>(type: "INTEGER", nullable: false),
                    RucniUnos = table.Column<bool>(type: "INTEGER", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Praznici", x => x.PraznikId);
                });

            migrationBuilder.CreateIndex(
                name: "IX_Praznici_Datum",
                table: "Praznici",
                column: "Datum",
                unique: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "Praznici");
        }
    }
}
