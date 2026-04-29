using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Cex.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class AddSignalRecordLastCheckedCandleAt : Migration
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
                oldDefaultValue: new DateTime(2026, 3, 25, 13, 25, 13, 556, DateTimeKind.Utc).AddTicks(8090));

            migrationBuilder.AlterColumn<DateTime>(
                name: "TakeProfitHitAt",
                table: "SignalRecords",
                type: "datetime2(0)",
                precision: 0,
                nullable: true,
                oldClrType: typeof(DateTime),
                oldType: "datetime2",
                oldNullable: true);

            migrationBuilder.AlterColumn<DateTime>(
                name: "StopLossHitAt",
                table: "SignalRecords",
                type: "datetime2(0)",
                precision: 0,
                nullable: true,
                oldClrType: typeof(DateTime),
                oldType: "datetime2",
                oldNullable: true);

            migrationBuilder.AlterColumn<DateTime>(
                name: "PreviousCandleAt",
                table: "SignalRecords",
                type: "datetime2(0)",
                precision: 0,
                nullable: false,
                oldClrType: typeof(DateTime),
                oldType: "datetime2");

            migrationBuilder.AlterColumn<DateTime>(
                name: "DetectedAt",
                table: "SignalRecords",
                type: "datetime2(0)",
                precision: 0,
                nullable: false,
                oldClrType: typeof(DateTime),
                oldType: "datetime2");

            migrationBuilder.AlterColumn<DateTime>(
                name: "CreatedAt",
                table: "SignalRecords",
                type: "datetime2(0)",
                precision: 0,
                nullable: false,
                defaultValueSql: "GETUTCDATE()",
                oldClrType: typeof(DateTime),
                oldType: "datetime2",
                oldDefaultValueSql: "GETUTCDATE()");

            migrationBuilder.AddColumn<DateTime>(
                name: "EntryHitAt",
                table: "SignalRecords",
                type: "datetime2(0)",
                precision: 0,
                nullable: true);

            migrationBuilder.AddColumn<DateTime>(
                name: "LastCheckedCandleAt",
                table: "SignalRecords",
                type: "datetime2(0)",
                precision: 0,
                nullable: false,
                defaultValueSql: "GETUTCDATE()");

            // Backfill: existing rows use CreatedAt as the initial pointer
            migrationBuilder.Sql("UPDATE SignalRecords SET LastCheckedCandleAt = CreatedAt");

            migrationBuilder.CreateIndex(
                name: "IX_SignalRecords_LastCheckedCandleAt",
                table: "SignalRecords",
                column: "LastCheckedCandleAt");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "IX_SignalRecords_LastCheckedCandleAt",
                table: "SignalRecords");

            migrationBuilder.DropColumn(
                name: "EntryHitAt",
                table: "SignalRecords");

            migrationBuilder.DropColumn(
                name: "LastCheckedCandleAt",
                table: "SignalRecords");

            migrationBuilder.AlterColumn<DateTime>(
                name: "CreatedAt",
                table: "TradeHistories",
                type: "datetime2",
                nullable: false,
                defaultValue: new DateTime(2026, 3, 25, 13, 25, 13, 556, DateTimeKind.Utc).AddTicks(8090),
                oldClrType: typeof(DateTime),
                oldType: "datetime2",
                oldDefaultValueSql: "GETUTCDATE()");

            migrationBuilder.AlterColumn<DateTime>(
                name: "TakeProfitHitAt",
                table: "SignalRecords",
                type: "datetime2",
                nullable: true,
                oldClrType: typeof(DateTime),
                oldType: "datetime2(0)",
                oldPrecision: 0,
                oldNullable: true);

            migrationBuilder.AlterColumn<DateTime>(
                name: "StopLossHitAt",
                table: "SignalRecords",
                type: "datetime2",
                nullable: true,
                oldClrType: typeof(DateTime),
                oldType: "datetime2(0)",
                oldPrecision: 0,
                oldNullable: true);

            migrationBuilder.AlterColumn<DateTime>(
                name: "PreviousCandleAt",
                table: "SignalRecords",
                type: "datetime2",
                nullable: false,
                oldClrType: typeof(DateTime),
                oldType: "datetime2(0)",
                oldPrecision: 0);

            migrationBuilder.AlterColumn<DateTime>(
                name: "DetectedAt",
                table: "SignalRecords",
                type: "datetime2",
                nullable: false,
                oldClrType: typeof(DateTime),
                oldType: "datetime2(0)",
                oldPrecision: 0);

            migrationBuilder.AlterColumn<DateTime>(
                name: "CreatedAt",
                table: "SignalRecords",
                type: "datetime2",
                nullable: false,
                defaultValueSql: "GETUTCDATE()",
                oldClrType: typeof(DateTime),
                oldType: "datetime2(0)",
                oldPrecision: 0,
                oldDefaultValueSql: "GETUTCDATE()");
        }
    }
}
