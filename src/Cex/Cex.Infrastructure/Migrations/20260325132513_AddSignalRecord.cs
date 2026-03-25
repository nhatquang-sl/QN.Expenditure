using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Cex.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class AddSignalRecord : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AlterColumn<DateTime>(
                name: "CreatedAt",
                table: "TradeHistories",
                type: "datetime2",
                nullable: false,
                defaultValue: new DateTime(2026, 3, 25, 13, 25, 13, 556, DateTimeKind.Utc).AddTicks(8090),
                oldClrType: typeof(DateTime),
                oldType: "datetime2",
                oldDefaultValue: new DateTime(2026, 1, 13, 9, 51, 51, 366, DateTimeKind.Utc).AddTicks(530));

            migrationBuilder.CreateTable(
                name: "SignalRecords",
                columns: table => new
                {
                    Id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    Symbol = table.Column<string>(type: "nvarchar(20)", maxLength: 20, nullable: false),
                    Interval = table.Column<string>(type: "nvarchar(20)", maxLength: 20, nullable: false),
                    SignalType = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    DetectedAt = table.Column<DateTime>(type: "datetime2", nullable: false),
                    PreviousCandleAt = table.Column<DateTime>(type: "datetime2", nullable: false),
                    RsiValue = table.Column<decimal>(type: "decimal(10,4)", precision: 10, scale: 4, nullable: false),
                    PreviousRsiValue = table.Column<decimal>(type: "decimal(10,4)", precision: 10, scale: 4, nullable: false),
                    EntryPrice = table.Column<decimal>(type: "decimal(18,8)", precision: 18, scale: 8, nullable: false),
                    StopLoss = table.Column<decimal>(type: "decimal(18,8)", precision: 18, scale: 8, nullable: false),
                    TakeProfit = table.Column<decimal>(type: "decimal(18,8)", precision: 18, scale: 8, nullable: false),
                    StopLossHitAt = table.Column<DateTime>(type: "datetime2", nullable: true),
                    TakeProfitHitAt = table.Column<DateTime>(type: "datetime2", nullable: true),
                    CreatedAt = table.Column<DateTime>(type: "datetime2", nullable: false, defaultValueSql: "GETUTCDATE()")
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_SignalRecords", x => x.Id);
                });

            migrationBuilder.CreateIndex(
                name: "IX_SignalRecords_Symbol_Interval_DetectedAt",
                table: "SignalRecords",
                columns: new[] { "Symbol", "Interval", "DetectedAt" },
                unique: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "SignalRecords");

            migrationBuilder.AlterColumn<DateTime>(
                name: "CreatedAt",
                table: "TradeHistories",
                type: "datetime2",
                nullable: false,
                defaultValue: new DateTime(2026, 1, 13, 9, 51, 51, 366, DateTimeKind.Utc).AddTicks(530),
                oldClrType: typeof(DateTime),
                oldType: "datetime2",
                oldDefaultValue: new DateTime(2026, 3, 25, 13, 25, 13, 556, DateTimeKind.Utc).AddTicks(8090));
        }
    }
}
