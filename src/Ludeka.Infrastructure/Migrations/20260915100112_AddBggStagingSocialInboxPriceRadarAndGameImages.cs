using System;
using Microsoft.EntityFrameworkCore.Migrations;
using Npgsql.EntityFrameworkCore.PostgreSQL.Metadata;

#nullable disable

namespace Ludeka.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class AddBggStagingSocialInboxPriceRadarAndGameImages : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<int>(
                name: "BggDiscoveryCount",
                table: "NightlyCatalogingExecutionLogs",
                type: "integer",
                nullable: false,
                defaultValue: 0);

            migrationBuilder.AddColumn<string>(
                name: "BackCoverImageUrl",
                table: "Games",
                type: "text",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "TableImageUrl",
                table: "Games",
                type: "text",
                nullable: true);

            migrationBuilder.CreateTable(
                name: "BggCatalogStaging",
                columns: table => new
                {
                    BggId = table.Column<int>(type: "integer", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    OriginalTitle = table.Column<string>(type: "character varying(250)", maxLength: 250, nullable: false),
                    SpanishTitle = table.Column<string>(type: "character varying(250)", maxLength: 250, nullable: true),
                    YearPublished = table.Column<int>(type: "integer", nullable: true),
                    BggRank = table.Column<int>(type: "integer", nullable: true),
                    UsersRated = table.Column<int>(type: "integer", nullable: false),
                    BayesAverage = table.Column<double>(type: "double precision", nullable: false),
                    AverageRating = table.Column<double>(type: "double precision", nullable: false),
                    FetchStatus = table.Column<int>(type: "integer", nullable: false),
                    ImagesStatus = table.Column<int>(type: "integer", nullable: false),
                    AiStatus = table.Column<int>(type: "integer", nullable: false),
                    PromotionStatus = table.Column<int>(type: "integer", nullable: false),
                    RawThingXml = table.Column<string>(type: "text", nullable: true),
                    Designer = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: true),
                    Publisher = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: true),
                    Description = table.Column<string>(type: "text", nullable: true),
                    MinPlayers = table.Column<int>(type: "integer", nullable: false),
                    MaxPlayers = table.Column<int>(type: "integer", nullable: false),
                    PlayingTimeMinutes = table.Column<int>(type: "integer", nullable: false),
                    MinAge = table.Column<int>(type: "integer", nullable: false),
                    CoverImageUrl = table.Column<string>(type: "text", nullable: true),
                    ThumbnailUrl = table.Column<string>(type: "text", nullable: true),
                    BackCoverImageUrl = table.Column<string>(type: "text", nullable: true),
                    TableImageUrl = table.Column<string>(type: "text", nullable: true),
                    AiSummaryJson = table.Column<string>(type: "text", nullable: true),
                    RetryCount = table.Column<int>(type: "integer", nullable: false),
                    ErrorMessage = table.Column<string>(type: "character varying(1000)", maxLength: 1000, nullable: true),
                    CreatedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    UpdatedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    ProcessedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_BggCatalogStaging", x => x.BggId);
                });

            migrationBuilder.CreateTable(
                name: "GamePriceSnapshots",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    GameId = table.Column<Guid>(type: "uuid", nullable: false),
                    StoreName = table.Column<string>(type: "character varying(150)", maxLength: 150, nullable: false),
                    AffiliateUrl = table.Column<string>(type: "character varying(500)", maxLength: 500, nullable: false),
                    Price = table.Column<decimal>(type: "numeric", nullable: false),
                    Currency = table.Column<string>(type: "character varying(10)", maxLength: 10, nullable: false),
                    InStock = table.Column<bool>(type: "boolean", nullable: false),
                    RecordedAtUtc = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_GamePriceSnapshots", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "MonitoredSocialAccounts",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    Name = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: false),
                    Platform = table.Column<int>(type: "integer", nullable: false),
                    HandleOrChannelId = table.Column<string>(type: "character varying(150)", maxLength: 150, nullable: false),
                    AccountType = table.Column<int>(type: "integer", nullable: false),
                    ProfileUrl = table.Column<string>(type: "character varying(500)", maxLength: 500, nullable: false),
                    IsEnabled = table.Column<bool>(type: "boolean", nullable: false),
                    LastCheckedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    ResolvedFeedUrl = table.Column<string>(type: "character varying(500)", maxLength: 500, nullable: true),
                    Notes = table.Column<string>(type: "character varying(1000)", maxLength: 1000, nullable: true),
                    CreatedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    UpdatedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_MonitoredSocialAccounts", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "SocialInboxItems",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    SourceUrl = table.Column<string>(type: "character varying(500)", maxLength: 500, nullable: false),
                    Platform = table.Column<int>(type: "integer", nullable: false),
                    DetectedType = table.Column<int>(type: "integer", nullable: false),
                    Status = table.Column<int>(type: "integer", nullable: false),
                    Title = table.Column<string>(type: "character varying(250)", maxLength: 250, nullable: false),
                    OrganizerOrAuthor = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: false),
                    Collaborator = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: true),
                    GameId = table.Column<Guid>(type: "uuid", nullable: true),
                    GameTitle = table.Column<string>(type: "text", nullable: true),
                    EventOrReleaseDate = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    EventEndDate = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    Location = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: true),
                    EstimatedPvp = table.Column<decimal>(type: "numeric", nullable: true),
                    MediaCategory = table.Column<int>(type: "integer", nullable: true),
                    PlayerCountBadge = table.Column<string>(type: "character varying(50)", maxLength: 50, nullable: true),
                    OriginalCaption = table.Column<string>(type: "text", nullable: true),
                    ThumbnailUrl = table.Column<string>(type: "character varying(1000)", maxLength: 1000, nullable: true),
                    IsVideo = table.Column<bool>(type: "boolean", nullable: false),
                    AiAnalysisNotes = table.Column<string>(type: "text", nullable: true),
                    CreatedEntityId = table.Column<Guid>(type: "uuid", nullable: true),
                    ModeratorNotes = table.Column<string>(type: "text", nullable: true),
                    CreatedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    ReviewedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    ReviewedByUserId = table.Column<string>(type: "text", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_SocialInboxItems", x => x.Id);
                    table.ForeignKey(
                        name: "FK_SocialInboxItems_Games_GameId",
                        column: x => x.GameId,
                        principalTable: "Games",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.SetNull);
                });

            migrationBuilder.CreateIndex(
                name: "IX_BggCatalogStaging_AiStatus",
                table: "BggCatalogStaging",
                column: "AiStatus");

            migrationBuilder.CreateIndex(
                name: "IX_BggCatalogStaging_CreatedAt",
                table: "BggCatalogStaging",
                column: "CreatedAt");

            migrationBuilder.CreateIndex(
                name: "IX_BggCatalogStaging_FetchStatus",
                table: "BggCatalogStaging",
                column: "FetchStatus");

            migrationBuilder.CreateIndex(
                name: "IX_BggCatalogStaging_ImagesStatus",
                table: "BggCatalogStaging",
                column: "ImagesStatus");

            migrationBuilder.CreateIndex(
                name: "IX_BggCatalogStaging_OriginalTitle",
                table: "BggCatalogStaging",
                column: "OriginalTitle");

            migrationBuilder.CreateIndex(
                name: "IX_BggCatalogStaging_PromotionStatus",
                table: "BggCatalogStaging",
                column: "PromotionStatus");

            migrationBuilder.CreateIndex(
                name: "IX_BggCatalogStaging_UsersRated",
                table: "BggCatalogStaging",
                column: "UsersRated");

            migrationBuilder.CreateIndex(
                name: "IX_GamePriceSnapshots_GameId_RecordedAtUtc",
                table: "GamePriceSnapshots",
                columns: new[] { "GameId", "RecordedAtUtc" });

            migrationBuilder.CreateIndex(
                name: "IX_GamePriceSnapshots_GameId_StoreName",
                table: "GamePriceSnapshots",
                columns: new[] { "GameId", "StoreName" });

            migrationBuilder.CreateIndex(
                name: "IX_GamePriceSnapshots_Price",
                table: "GamePriceSnapshots",
                column: "Price");

            migrationBuilder.CreateIndex(
                name: "IX_MonitoredSocialAccounts_IsEnabled",
                table: "MonitoredSocialAccounts",
                column: "IsEnabled");

            migrationBuilder.CreateIndex(
                name: "IX_MonitoredSocialAccounts_Platform_HandleOrChannelId",
                table: "MonitoredSocialAccounts",
                columns: new[] { "Platform", "HandleOrChannelId" });

            migrationBuilder.CreateIndex(
                name: "IX_SocialInboxItems_CreatedAt",
                table: "SocialInboxItems",
                column: "CreatedAt");

            migrationBuilder.CreateIndex(
                name: "IX_SocialInboxItems_DetectedType",
                table: "SocialInboxItems",
                column: "DetectedType");

            migrationBuilder.CreateIndex(
                name: "IX_SocialInboxItems_GameId",
                table: "SocialInboxItems",
                column: "GameId");

            migrationBuilder.CreateIndex(
                name: "IX_SocialInboxItems_Status",
                table: "SocialInboxItems",
                column: "Status");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "BggCatalogStaging");

            migrationBuilder.DropTable(
                name: "GamePriceSnapshots");

            migrationBuilder.DropTable(
                name: "MonitoredSocialAccounts");

            migrationBuilder.DropTable(
                name: "SocialInboxItems");

            migrationBuilder.DropColumn(
                name: "BggDiscoveryCount",
                table: "NightlyCatalogingExecutionLogs");

            migrationBuilder.DropColumn(
                name: "BackCoverImageUrl",
                table: "Games");

            migrationBuilder.DropColumn(
                name: "TableImageUrl",
                table: "Games");
        }
    }
}
