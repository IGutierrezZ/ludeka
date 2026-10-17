using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Ludeka.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class AddInstagramReelSyncItems : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "InstagramReelSyncItems",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    InstagramMediaId = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: false),
                    InstagramPermalink = table.Column<string>(type: "character varying(500)", maxLength: 500, nullable: false),
                    Caption = table.Column<string>(type: "text", nullable: true),
                    MediaPublishedAtUtc = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    Status = table.Column<int>(type: "integer", nullable: false),
                    GeneratedYouTubeTitle = table.Column<string>(type: "character varying(120)", maxLength: 120, nullable: true),
                    GeneratedYouTubeDescription = table.Column<string>(type: "text", nullable: true),
                    YouTubeVideoId = table.Column<string>(type: "character varying(64)", maxLength: 64, nullable: true),
                    YouTubeUrl = table.Column<string>(type: "character varying(500)", maxLength: 500, nullable: true),
                    AttemptCount = table.Column<int>(type: "integer", nullable: false),
                    LastError = table.Column<string>(type: "character varying(1000)", maxLength: 1000, nullable: true),
                    CreatedAtUtc = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    SyncedAtUtc = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_InstagramReelSyncItems", x => x.Id);
                });

            migrationBuilder.CreateIndex(
                name: "IX_InstagramReelSyncItems_InstagramMediaId",
                table: "InstagramReelSyncItems",
                column: "InstagramMediaId",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_InstagramReelSyncItems_Status",
                table: "InstagramReelSyncItems",
                column: "Status");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "InstagramReelSyncItems");
        }
    }
}
