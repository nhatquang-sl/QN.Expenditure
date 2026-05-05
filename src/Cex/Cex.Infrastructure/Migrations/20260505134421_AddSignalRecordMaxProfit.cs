using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Cex.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class AddSignalRecordMaxProfit : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<int>(
                name: "Leverage",
                table: "SignalRecords",
                type: "int",
                nullable: false,
                defaultValue: 10);

            migrationBuilder.AddColumn<decimal>(
                name: "MaxProfit",
                table: "SignalRecords",
                type: "decimal(10,4)",
                precision: 10,
                scale: 4,
                nullable: false,
                defaultValue: 0m);

            migrationBuilder.AddColumn<DateTime>(
                name: "MaxProfitCheckedAt",
                table: "SignalRecords",
                type: "datetime2(0)",
                precision: 0,
                nullable: true);

            migrationBuilder.AddColumn<DateTime>(
                name: "MaxProfitHitAt",
                table: "SignalRecords",
                type: "datetime2(0)",
                precision: 0,
                nullable: true);

            migrationBuilder.CreateIndex(
                name: "IX_SignalRecords_MaxProfitCheckedAt",
                table: "SignalRecords",
                column: "MaxProfitCheckedAt");

            migrationBuilder.AddCheckConstraint(
                name: "CK_SignalRecords_Leverage",
                table: "SignalRecords",
                sql: "[Leverage] >= 1 AND [Leverage] <= 125");

            // Backfill: set MaxProfitCheckedAt only for already-entered signals.
            migrationBuilder.Sql("UPDATE SignalRecords SET MaxProfitCheckedAt = EntryHitAt WHERE EntryHitAt IS NOT NULL");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "IX_SignalRecords_MaxProfitCheckedAt",
                table: "SignalRecords");

            migrationBuilder.DropCheckConstraint(
                name: "CK_SignalRecords_Leverage",
                table: "SignalRecords");

            migrationBuilder.DropColumn(
                name: "Leverage",
                table: "SignalRecords");

            migrationBuilder.DropColumn(
                name: "MaxProfit",
                table: "SignalRecords");

            migrationBuilder.DropColumn(
                name: "MaxProfitCheckedAt",
                table: "SignalRecords");

            migrationBuilder.DropColumn(
                name: "MaxProfitHitAt",
                table: "SignalRecords");
        }
    }
}
