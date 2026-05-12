using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Cex.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class AddSignal : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AlterColumn<DateTime>(
                name: "CreatedAt",
                table: "TradeHistories",
                type: "datetime2",
                nullable: false,
                defaultValueSql: "GETUTCDATE()",
                oldClrType: typeof(DateTime),
                oldType: "datetime2",
                oldDefaultValue: new DateTime(2026, 1, 13, 9, 51, 51, 366, DateTimeKind.Utc).AddTicks(530));

            migrationBuilder.CreateTable(
                name: "Signals",
                columns: table => new
                {
                    Id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    Symbol = table.Column<string>(type: "nvarchar(20)", maxLength: 20, nullable: false),
                    Interval = table.Column<string>(type: "nvarchar(20)", maxLength: 20, nullable: false),
                    SignalType = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    DetectedAt = table.Column<DateTime>(type: "datetime2(0)", precision: 0, nullable: false),
                    PreviousCandleAt = table.Column<DateTime>(type: "datetime2(0)", precision: 0, nullable: false),
                    RsiValue = table.Column<decimal>(type: "decimal(10,4)", precision: 10, scale: 4, nullable: false),
                    PreviousRsiValue = table.Column<decimal>(type: "decimal(10,4)", precision: 10, scale: 4, nullable: false),
                    EntryPrice = table.Column<decimal>(type: "decimal(18,8)", precision: 18, scale: 8, nullable: false),
                    StopLoss = table.Column<decimal>(type: "decimal(18,8)", precision: 18, scale: 8, nullable: false),
                    TakeProfit = table.Column<decimal>(type: "decimal(18,8)", precision: 18, scale: 8, nullable: false),
                    Leverage = table.Column<int>(type: "int", nullable: false, defaultValue: 10),
                    MaxProfit = table.Column<decimal>(type: "decimal(10,4)", precision: 10, scale: 4, nullable: false, defaultValue: 0m),
                    MaxProfitHitAt = table.Column<DateTime>(type: "datetime2(0)", precision: 0, nullable: true),
                    MaxProfitCheckedAt = table.Column<DateTime>(type: "datetime2(0)", precision: 0, nullable: true),
                    EntryHitAt = table.Column<DateTime>(type: "datetime2(0)", precision: 0, nullable: true),
                    StopLossHitAt = table.Column<DateTime>(type: "datetime2(0)", precision: 0, nullable: true),
                    TakeProfitHitAt = table.Column<DateTime>(type: "datetime2(0)", precision: 0, nullable: true),
                    CreatedAt = table.Column<DateTime>(type: "datetime2(0)", precision: 0, nullable: false, defaultValueSql: "GETUTCDATE()"),
                    LastCheckedCandleAt = table.Column<DateTime>(type: "datetime2(0)", precision: 0, nullable: false, defaultValueSql: "GETUTCDATE()")
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Signals", x => x.Id);
                    table.CheckConstraint("CK_Signals_Leverage", "[Leverage] >= 1 AND [Leverage] <= 125");
                });

            migrationBuilder.CreateIndex(
                name: "IX_Signals_LastCheckedCandleAt",
                table: "Signals",
                column: "LastCheckedCandleAt");

            migrationBuilder.CreateIndex(
                name: "IX_Signals_MaxProfitCheckedAt",
                table: "Signals",
                column: "MaxProfitCheckedAt");

            migrationBuilder.CreateIndex(
                name: "IX_Signals_Symbol_Interval_DetectedAt",
                table: "Signals",
                columns: new[] { "Symbol", "Interval", "DetectedAt" },
                unique: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "Signals");

            migrationBuilder.AlterColumn<DateTime>(
                name: "CreatedAt",
                table: "TradeHistories",
                type: "datetime2",
                nullable: false,
                defaultValue: new DateTime(2026, 1, 13, 9, 51, 51, 366, DateTimeKind.Utc).AddTicks(530),
                oldClrType: typeof(DateTime),
                oldType: "datetime2",
                oldDefaultValueSql: "GETUTCDATE()");
        }
    }
}
