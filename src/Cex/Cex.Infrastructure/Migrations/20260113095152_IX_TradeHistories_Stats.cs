using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Cex.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class IX_TradeHistories_Stats : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AlterColumn<string>(
                name: "Symbol",
                table: "TradeHistories",
                type: "nvarchar(450)",
                nullable: false,
                oldClrType: typeof(string),
                oldType: "nvarchar(max)");

            migrationBuilder.AlterColumn<string>(
                name: "Side",
                table: "TradeHistories",
                type: "nvarchar(450)",
                nullable: false,
                oldClrType: typeof(string),
                oldType: "nvarchar(max)");

            migrationBuilder.AlterColumn<DateTime>(
                name: "CreatedAt",
                table: "TradeHistories",
                type: "datetime2",
                nullable: false,
                defaultValue: new DateTime(2026, 1, 13, 9, 51, 51, 366, DateTimeKind.Utc).AddTicks(530),
                oldClrType: typeof(DateTime),
                oldType: "datetime2",
                oldDefaultValue: new DateTime(2025, 12, 12, 11, 21, 45, 813, DateTimeKind.Utc).AddTicks(6920));

            migrationBuilder.CreateIndex(
                name: "IX_TradeHistories_Stats",
                table: "TradeHistories",
                columns: new[] { "UserId", "Symbol", "Side" })
                .Annotation("SqlServer:Include", new[] { "Funds", "Fee", "Size" });

            migrationBuilder.CreateIndex(
                name: "IX_TradeHistories_UserId_Symbol_TradedAt",
                table: "TradeHistories",
                columns: new[] { "UserId", "Symbol", "TradedAt" },
                descending: new[] { false, false, true });
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "IX_TradeHistories_Stats",
                table: "TradeHistories");

            migrationBuilder.DropIndex(
                name: "IX_TradeHistories_UserId_Symbol_TradedAt",
                table: "TradeHistories");

            migrationBuilder.AlterColumn<string>(
                name: "Symbol",
                table: "TradeHistories",
                type: "nvarchar(max)",
                nullable: false,
                oldClrType: typeof(string),
                oldType: "nvarchar(450)");

            migrationBuilder.AlterColumn<string>(
                name: "Side",
                table: "TradeHistories",
                type: "nvarchar(max)",
                nullable: false,
                oldClrType: typeof(string),
                oldType: "nvarchar(450)");

            migrationBuilder.AlterColumn<DateTime>(
                name: "CreatedAt",
                table: "TradeHistories",
                type: "datetime2",
                nullable: false,
                defaultValue: new DateTime(2025, 12, 12, 11, 21, 45, 813, DateTimeKind.Utc).AddTicks(6920),
                oldClrType: typeof(DateTime),
                oldType: "datetime2",
                oldDefaultValue: new DateTime(2026, 1, 13, 9, 51, 51, 366, DateTimeKind.Utc).AddTicks(530));
        }
    }
}
