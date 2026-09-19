using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Ludeka.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class AddNotificationLogDeliveryColumns : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<int>(
                name: "Attempts",
                table: "NotificationLogs",
                type: "integer",
                nullable: false,
                defaultValue: 0);

            migrationBuilder.AddColumn<Guid>(
                name: "MessageId",
                table: "NotificationLogs",
                type: "uuid",
                nullable: true);

            migrationBuilder.AddColumn<DateTimeOffset>(
                name: "NextAttemptAt",
                table: "NotificationLogs",
                type: "timestamp with time zone",
                nullable: true);

            migrationBuilder.CreateIndex(
                name: "IX_NotificationLogs_MessageId_Channel",
                table: "NotificationLogs",
                columns: new[] { "MessageId", "Channel" },
                unique: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "IX_NotificationLogs_MessageId_Channel",
                table: "NotificationLogs");

            migrationBuilder.DropColumn(
                name: "Attempts",
                table: "NotificationLogs");

            migrationBuilder.DropColumn(
                name: "MessageId",
                table: "NotificationLogs");

            migrationBuilder.DropColumn(
                name: "NextAttemptAt",
                table: "NotificationLogs");
        }
    }
}
