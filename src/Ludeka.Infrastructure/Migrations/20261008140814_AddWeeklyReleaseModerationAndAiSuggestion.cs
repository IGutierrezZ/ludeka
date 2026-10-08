using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Ludeka.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class AddWeeklyReleaseModerationAndAiSuggestion : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "AiMatchReasoning",
                table: "WeeklyReleases",
                type: "character varying(2000)",
                maxLength: 2000,
                nullable: true);

            migrationBuilder.AddColumn<int>(
                name: "AiSuggestedBggId",
                table: "WeeklyReleases",
                type: "integer",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "AiSuggestedTitle",
                table: "WeeklyReleases",
                type: "character varying(300)",
                maxLength: 300,
                nullable: true);

            migrationBuilder.AddColumn<int>(
                name: "Status",
                table: "WeeklyReleases",
                type: "integer",
                nullable: false,
                defaultValue: 1);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "AiMatchReasoning",
                table: "WeeklyReleases");

            migrationBuilder.DropColumn(
                name: "AiSuggestedBggId",
                table: "WeeklyReleases");

            migrationBuilder.DropColumn(
                name: "AiSuggestedTitle",
                table: "WeeklyReleases");

            migrationBuilder.DropColumn(
                name: "Status",
                table: "WeeklyReleases");
        }
    }
}
