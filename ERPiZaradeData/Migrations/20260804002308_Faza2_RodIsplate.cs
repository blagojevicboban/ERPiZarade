using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace ERPiZaradeData.Migrations
{
    /// <inheritdoc />
    public partial class Faza2_RodIsplate : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<int>(
                name: "Rod",
                table: "Isplate",
                type: "INTEGER",
                nullable: false,
                defaultValue: 0);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "Rod",
                table: "Isplate");
        }
    }
}
