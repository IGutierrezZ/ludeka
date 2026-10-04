using System;
using System.Collections.Generic;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Ludeka.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class AddGameEanAndAffiliateClicks : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<List<string>>(
                name: "AdditionalBarcodes",
                table: "Games",
                type: "text[]",
                nullable: false);

            migrationBuilder.AddColumn<string>(
                name: "Ean",
                table: "Games",
                type: "character varying(14)",
                maxLength: 14,
                nullable: true);

            migrationBuilder.CreateTable(
                name: "AffiliateClicks",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    GameId = table.Column<Guid>(type: "uuid", nullable: true),
                    GameTitle = table.Column<string>(type: "character varying(250)", maxLength: 250, nullable: false),
                    GameSlug = table.Column<string>(type: "character varying(250)", maxLength: 250, nullable: false),
                    StoreName = table.Column<string>(type: "character varying(150)", maxLength: 150, nullable: false),
                    TargetUrl = table.Column<string>(type: "character varying(1000)", maxLength: 1000, nullable: false),
                    Country = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: true),
                    ClickedAtUtc = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_AffiliateClicks", x => x.Id);
                });

            migrationBuilder.CreateIndex(
                name: "IX_Games_Ean",
                table: "Games",
                column: "Ean");

            migrationBuilder.CreateIndex(
                name: "IX_AffiliateClicks_ClickedAtUtc",
                table: "AffiliateClicks",
                column: "ClickedAtUtc");

            migrationBuilder.CreateIndex(
                name: "IX_AffiliateClicks_GameId",
                table: "AffiliateClicks",
                column: "GameId");

            migrationBuilder.CreateIndex(
                name: "IX_AffiliateClicks_GameSlug",
                table: "AffiliateClicks",
                column: "GameSlug");

            migrationBuilder.CreateIndex(
                name: "IX_AffiliateClicks_StoreName",
                table: "AffiliateClicks",
                column: "StoreName");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "AffiliateClicks");

            migrationBuilder.DropIndex(
                name: "IX_Games_Ean",
                table: "Games");

            migrationBuilder.DropColumn(
                name: "AdditionalBarcodes",
                table: "Games");

            migrationBuilder.DropColumn(
                name: "Ean",
                table: "Games");
        }
    }
}
