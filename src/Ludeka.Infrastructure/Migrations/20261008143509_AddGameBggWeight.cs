using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Ludeka.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class AddGameBggWeight : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<double>(
                name: "BggWeight",
                table: "Games",
                type: "double precision",
                nullable: true);

            migrationBuilder.CreateIndex(
                name: "IX_Games_BggWeight",
                table: "Games",
                column: "BggWeight");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "IX_Games_BggWeight",
                table: "Games");

            migrationBuilder.DropColumn(
                name: "BggWeight",
                table: "Games");
        }
    }
}
