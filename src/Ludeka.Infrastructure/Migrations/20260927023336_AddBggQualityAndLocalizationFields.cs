using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Ludeka.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class AddBggQualityAndLocalizationFields : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "LocalizedTitles",
                table: "Games",
                type: "jsonb",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "RegionalPublishers",
                table: "Games",
                type: "jsonb",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "SpanishPublisher",
                table: "Games",
                type: "character varying(200)",
                maxLength: 200,
                nullable: true);

            migrationBuilder.AddColumn<int>(
                name: "InferredFootprint",
                table: "BggCatalogStaging",
                type: "integer",
                nullable: false,
                defaultValue: 0);

            migrationBuilder.AddColumn<int>(
                name: "MaxPlayTimeMinutes",
                table: "BggCatalogStaging",
                type: "integer",
                nullable: false,
                defaultValue: 0);

            migrationBuilder.AddColumn<int>(
                name: "MinPlayTimeMinutes",
                table: "BggCatalogStaging",
                type: "integer",
                nullable: false,
                defaultValue: 0);

            migrationBuilder.AddColumn<string>(
                name: "RegionalPublishersJson",
                table: "BggCatalogStaging",
                type: "text",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "ScalabilityJson",
                table: "BggCatalogStaging",
                type: "text",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "SleevesJson",
                table: "BggCatalogStaging",
                type: "text",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "SpanishPublisher",
                table: "BggCatalogStaging",
                type: "character varying(200)",
                maxLength: 200,
                nullable: true);

            migrationBuilder.CreateIndex(
                name: "IX_Games_SpanishPublisher",
                table: "Games",
                column: "SpanishPublisher");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "IX_Games_SpanishPublisher",
                table: "Games");

            migrationBuilder.DropColumn(
                name: "LocalizedTitles",
                table: "Games");

            migrationBuilder.DropColumn(
                name: "RegionalPublishers",
                table: "Games");

            migrationBuilder.DropColumn(
                name: "SpanishPublisher",
                table: "Games");

            migrationBuilder.DropColumn(
                name: "InferredFootprint",
                table: "BggCatalogStaging");

            migrationBuilder.DropColumn(
                name: "MaxPlayTimeMinutes",
                table: "BggCatalogStaging");

            migrationBuilder.DropColumn(
                name: "MinPlayTimeMinutes",
                table: "BggCatalogStaging");

            migrationBuilder.DropColumn(
                name: "RegionalPublishersJson",
                table: "BggCatalogStaging");

            migrationBuilder.DropColumn(
                name: "ScalabilityJson",
                table: "BggCatalogStaging");

            migrationBuilder.DropColumn(
                name: "SleevesJson",
                table: "BggCatalogStaging");

            migrationBuilder.DropColumn(
                name: "SpanishPublisher",
                table: "BggCatalogStaging");
        }
    }
}
