using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Ludeka.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class AddDailyTrendingGames : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "DailyTrendingGames",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    DateUtc = table.Column<DateOnly>(type: "date", nullable: false),
                    Rank = table.Column<int>(type: "integer", nullable: false),
                    BggId = table.Column<int>(type: "integer", nullable: false),
                    Title = table.Column<string>(type: "character varying(250)", maxLength: 250, nullable: false),
                    YearPublished = table.Column<int>(type: "integer", nullable: true),
                    ThumbnailUrl = table.Column<string>(type: "character varying(500)", maxLength: 500, nullable: true),
                    GameId = table.Column<Guid>(type: "uuid", nullable: true),
                    CreatedAtUtc = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_DailyTrendingGames", x => x.Id);
                    table.ForeignKey(
                        name: "FK_DailyTrendingGames_Games_GameId",
                        column: x => x.GameId,
                        principalTable: "Games",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.SetNull);
                });

            migrationBuilder.CreateIndex(
                name: "IX_DailyTrendingGames_BggId",
                table: "DailyTrendingGames",
                column: "BggId");

            migrationBuilder.CreateIndex(
                name: "IX_DailyTrendingGames_DateUtc",
                table: "DailyTrendingGames",
                column: "DateUtc");

            migrationBuilder.CreateIndex(
                name: "IX_DailyTrendingGames_DateUtc_BggId",
                table: "DailyTrendingGames",
                columns: new[] { "DateUtc", "BggId" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_DailyTrendingGames_DateUtc_Rank",
                table: "DailyTrendingGames",
                columns: new[] { "DateUtc", "Rank" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_DailyTrendingGames_GameId",
                table: "DailyTrendingGames",
                column: "GameId");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "DailyTrendingGames");
        }
    }
}
