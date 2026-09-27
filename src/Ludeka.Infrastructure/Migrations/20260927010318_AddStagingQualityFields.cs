using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Ludeka.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class AddStagingQualityFields : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<int>(
                name: "InferredFootprint",
                table: "BggCatalogStaging",
                type: "integer",
                nullable: false,
                defaultValue: 1);

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
                name: "ScalabilityJson",
                table: "BggCatalogStaging",
                type: "text",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "SleevesJson",
                table: "BggCatalogStaging",
                type: "text",
                nullable: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
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
                name: "ScalabilityJson",
                table: "BggCatalogStaging");

            migrationBuilder.DropColumn(
                name: "SleevesJson",
                table: "BggCatalogStaging");
        }
    }
}
