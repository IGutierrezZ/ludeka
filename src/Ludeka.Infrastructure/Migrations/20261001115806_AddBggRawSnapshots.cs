using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Ludeka.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class AddBggRawSnapshots : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "BggRawSnapshots",
                columns: table => new
                {
                    BggId = table.Column<int>(type: "integer", nullable: false),
                    RawJson = table.Column<string>(type: "jsonb", nullable: false),
                    ApiVersion = table.Column<int>(type: "integer", nullable: false),
                    FetchedAtUtc = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    UpdatedAtUtc = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_BggRawSnapshots", x => x.BggId);
                });

            migrationBuilder.CreateIndex(
                name: "IX_BggRawSnapshots_FetchedAtUtc",
                table: "BggRawSnapshots",
                column: "FetchedAtUtc");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "BggRawSnapshots");
        }
    }
}
