using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Ludeka.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class AddAffiliateFeedSourcesAndDiscrepancies : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "AffiliateEanDiscrepancies",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    GameId = table.Column<Guid>(type: "uuid", nullable: false),
                    GameTitle = table.Column<string>(type: "character varying(250)", maxLength: 250, nullable: false),
                    GameSlug = table.Column<string>(type: "character varying(250)", maxLength: 250, nullable: false),
                    CurrentEan = table.Column<string>(type: "character varying(14)", maxLength: 14, nullable: true),
                    FeedEan = table.Column<string>(type: "character varying(14)", maxLength: 14, nullable: false),
                    StoreName = table.Column<string>(type: "character varying(150)", maxLength: 150, nullable: false),
                    DetectedAtUtc = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    IsResolved = table.Column<bool>(type: "boolean", nullable: false),
                    ResolutionNote = table.Column<string>(type: "character varying(500)", maxLength: 500, nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_AffiliateEanDiscrepancies", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "AffiliateFeedSources",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    StoreName = table.Column<string>(type: "character varying(150)", maxLength: 150, nullable: false),
                    FeedUrl = table.Column<string>(type: "character varying(1000)", maxLength: 1000, nullable: false),
                    Format = table.Column<int>(type: "integer", nullable: false),
                    AffiliateTag = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: true),
                    Country = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: false),
                    IsEnabled = table.Column<bool>(type: "boolean", nullable: false),
                    SyncIntervalHours = table.Column<int>(type: "integer", nullable: false),
                    LastSyncUtc = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    LastSyncStatus = table.Column<string>(type: "character varying(250)", maxLength: 250, nullable: true),
                    MatchedProductsCount = table.Column<int>(type: "integer", nullable: false),
                    CreatedAtUtc = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_AffiliateFeedSources", x => x.Id);
                });

            migrationBuilder.CreateIndex(
                name: "IX_AffiliateEanDiscrepancies_DetectedAtUtc",
                table: "AffiliateEanDiscrepancies",
                column: "DetectedAtUtc");

            migrationBuilder.CreateIndex(
                name: "IX_AffiliateEanDiscrepancies_FeedEan",
                table: "AffiliateEanDiscrepancies",
                column: "FeedEan");

            migrationBuilder.CreateIndex(
                name: "IX_AffiliateEanDiscrepancies_GameId",
                table: "AffiliateEanDiscrepancies",
                column: "GameId");

            migrationBuilder.CreateIndex(
                name: "IX_AffiliateEanDiscrepancies_IsResolved",
                table: "AffiliateEanDiscrepancies",
                column: "IsResolved");

            migrationBuilder.CreateIndex(
                name: "IX_AffiliateFeedSources_IsEnabled",
                table: "AffiliateFeedSources",
                column: "IsEnabled");

            migrationBuilder.CreateIndex(
                name: "IX_AffiliateFeedSources_StoreName",
                table: "AffiliateFeedSources",
                column: "StoreName");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "AffiliateEanDiscrepancies");

            migrationBuilder.DropTable(
                name: "AffiliateFeedSources");
        }
    }
}
