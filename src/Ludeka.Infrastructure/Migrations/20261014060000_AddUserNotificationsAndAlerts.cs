using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Ludeka.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class AddUserNotificationsAndAlerts : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<bool>(
                name: "NotifyNewGiveaways",
                table: "UserPreferences",
                type: "boolean",
                nullable: false,
                defaultValue: false);

            migrationBuilder.CreateTable(
                name: "UserNotifications",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    UserId = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: false),
                    Type = table.Column<int>(type: "integer", nullable: false),
                    Title = table.Column<string>(type: "character varying(250)", maxLength: 250, nullable: false),
                    Message = table.Column<string>(type: "character varying(1000)", maxLength: 1000, nullable: false),
                    TargetUrl = table.Column<string>(type: "character varying(500)", maxLength: 500, nullable: false),
                    GameId = table.Column<Guid>(type: "uuid", nullable: true),
                    ImageUrl = table.Column<string>(type: "character varying(1000)", maxLength: 1000, nullable: true),
                    IsRead = table.Column<bool>(type: "boolean", nullable: false, defaultValue: false),
                    CreatedAtUtc = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    ReadAtUtc = table.Column<DateTime>(type: "timestamp with time zone", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_UserNotifications", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "UserGameAlertSubscriptions",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    UserId = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: false),
                    GameId = table.Column<Guid>(type: "uuid", nullable: false),
                    AlertType = table.Column<int>(type: "integer", nullable: false),
                    ReferenceBasePvp = table.Column<decimal>(type: "numeric", nullable: true),
                    LastNotifiedPrice = table.Column<decimal>(type: "numeric", nullable: true),
                    LastNotifiedAtUtc = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    CreatedAtUtc = table.Column<DateTime>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_UserGameAlertSubscriptions", x => x.Id);
                    table.ForeignKey(
                        name: "FK_UserGameAlertSubscriptions_Games_GameId",
                        column: x => x.GameId,
                        principalTable: "Games",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "UserPushSubscriptions",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    UserId = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: false),
                    Endpoint = table.Column<string>(type: "character varying(1000)", maxLength: 1000, nullable: false),
                    P256dh = table.Column<string>(type: "character varying(250)", maxLength: 250, nullable: false),
                    Auth = table.Column<string>(type: "character varying(150)", maxLength: 150, nullable: false),
                    UserAgent = table.Column<string>(type: "character varying(500)", maxLength: 500, nullable: true),
                    CreatedAtUtc = table.Column<DateTime>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_UserPushSubscriptions", x => x.Id);
                });

            migrationBuilder.CreateIndex(
                name: "IX_UserNotifications_GameId",
                table: "UserNotifications",
                column: "GameId");

            migrationBuilder.CreateIndex(
                name: "IX_UserNotifications_UserId_IsRead_CreatedAtUtc",
                table: "UserNotifications",
                columns: new[] { "UserId", "IsRead", "CreatedAtUtc" });

            migrationBuilder.CreateIndex(
                name: "IX_UserGameAlertSubscriptions_GameId_AlertType",
                table: "UserGameAlertSubscriptions",
                columns: new[] { "GameId", "AlertType" });

            migrationBuilder.CreateIndex(
                name: "IX_UserGameAlertSubscriptions_UserId_GameId_AlertType",
                table: "UserGameAlertSubscriptions",
                columns: new[] { "UserId", "GameId", "AlertType" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_UserPushSubscriptions_UserId",
                table: "UserPushSubscriptions",
                column: "UserId");

            migrationBuilder.CreateIndex(
                name: "IX_UserPushSubscriptions_UserId_Endpoint",
                table: "UserPushSubscriptions",
                columns: new[] { "UserId", "Endpoint" },
                unique: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "UserNotifications");

            migrationBuilder.DropTable(
                name: "UserGameAlertSubscriptions");

            migrationBuilder.DropTable(
                name: "UserPushSubscriptions");

            migrationBuilder.DropColumn(
                name: "NotifyNewGiveaways",
                table: "UserPreferences");
        }
    }
}
