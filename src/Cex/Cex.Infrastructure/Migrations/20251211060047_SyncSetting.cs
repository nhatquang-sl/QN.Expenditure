using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Cex.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class SyncSetting : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AlterColumn<DateTime>(
                name: "CreatedAt",
                table: "TradeHistories",
                type: "datetime2",
                nullable: false,
                defaultValue: new DateTime(2025, 12, 11, 6, 0, 46, 979, DateTimeKind.Utc).AddTicks(6120),
                oldClrType: typeof(DateTime),
                oldType: "datetime2",
                oldDefaultValue: new DateTime(2025, 12, 5, 14, 20, 54, 769, DateTimeKind.Utc).AddTicks(1870));

            migrationBuilder.CreateTable(
                name: "SyncSettings",
                columns: table => new
                {
                    UserId = table.Column<string>(type: "nvarchar(36)", maxLength: 36, nullable: false),
                    Symbol = table.Column<string>(type: "nvarchar(50)", maxLength: 50, nullable: false),
                    StartSync = table.Column<DateTime>(type: "datetime2", nullable: false),
                    LastSync = table.Column<DateTime>(type: "datetime2", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_SyncSettings", x => new { x.UserId, x.Symbol });
                });

            migrationBuilder.CreateIndex(
                name: "IX_SyncSettings_UserId",
                table: "SyncSettings",
                column: "UserId");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "SyncSettings");

            migrationBuilder.AlterColumn<DateTime>(
                name: "CreatedAt",
                table: "TradeHistories",
                type: "datetime2",
                nullable: false,
                defaultValue: new DateTime(2025, 12, 5, 14, 20, 54, 769, DateTimeKind.Utc).AddTicks(1870),
                oldClrType: typeof(DateTime),
                oldType: "datetime2",
                oldDefaultValue: new DateTime(2025, 12, 11, 6, 0, 46, 979, DateTimeKind.Utc).AddTicks(6120));
        }
    }
}
