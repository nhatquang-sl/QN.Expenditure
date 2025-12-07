using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Cex.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class ExchangeSetting : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "ExchangeConfigs");

            migrationBuilder.AlterColumn<DateTime>(
                name: "CreatedAt",
                table: "TradeHistories",
                type: "datetime2",
                nullable: false,
                defaultValue: new DateTime(2025, 12, 5, 14, 20, 54, 769, DateTimeKind.Utc).AddTicks(1870),
                oldClrType: typeof(DateTime),
                oldType: "datetime2",
                oldDefaultValue: new DateTime(2025, 12, 2, 11, 34, 44, 602, DateTimeKind.Utc).AddTicks(4460));

            migrationBuilder.CreateTable(
                name: "ExchangeSettings",
                columns: table => new
                {
                    UserId = table.Column<string>(type: "nvarchar(36)", maxLength: 36, nullable: false),
                    ExchangeName = table.Column<string>(type: "nvarchar(50)", maxLength: 50, nullable: false),
                    ApiKey = table.Column<string>(type: "nvarchar(500)", maxLength: 500, nullable: false),
                    Secret = table.Column<string>(type: "nvarchar(500)", maxLength: 500, nullable: false),
                    Passphrase = table.Column<string>(type: "nvarchar(500)", maxLength: 500, nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_ExchangeSettings", x => new { x.UserId, x.ExchangeName });
                });

            migrationBuilder.CreateIndex(
                name: "IX_ExchangeSettings_UserId",
                table: "ExchangeSettings",
                column: "UserId");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "ExchangeSettings");

            migrationBuilder.AlterColumn<DateTime>(
                name: "CreatedAt",
                table: "TradeHistories",
                type: "datetime2",
                nullable: false,
                defaultValue: new DateTime(2025, 12, 2, 11, 34, 44, 602, DateTimeKind.Utc).AddTicks(4460),
                oldClrType: typeof(DateTime),
                oldType: "datetime2",
                oldDefaultValue: new DateTime(2025, 12, 5, 14, 20, 54, 769, DateTimeKind.Utc).AddTicks(1870));

            migrationBuilder.CreateTable(
                name: "ExchangeConfigs",
                columns: table => new
                {
                    UserId = table.Column<string>(type: "nvarchar(36)", maxLength: 36, nullable: false),
                    ExchangeName = table.Column<string>(type: "nvarchar(50)", maxLength: 50, nullable: false),
                    ApiKey = table.Column<string>(type: "nvarchar(500)", maxLength: 500, nullable: false),
                    Passphrase = table.Column<string>(type: "nvarchar(500)", maxLength: 500, nullable: true),
                    Secret = table.Column<string>(type: "nvarchar(500)", maxLength: 500, nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_ExchangeConfigs", x => new { x.UserId, x.ExchangeName });
                });

            migrationBuilder.CreateIndex(
                name: "IX_ExchangeConfigs_UserId",
                table: "ExchangeConfigs",
                column: "UserId");
        }
    }
}
