using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Ludeka.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class AddWeeklyReleaseSourceUrl : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "SourceUrl",
                table: "WeeklyReleases",
                type: "character varying(1000)",
                maxLength: 1000,
                nullable: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "SourceUrl",
                table: "WeeklyReleases");
        }
    }
}
