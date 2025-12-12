using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Cex.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class TradeHistory : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            // Step 1: Drop the primary key constraint
            migrationBuilder.DropPrimaryKey(
                name: "PK_TradeHistories",
                table: "TradeHistories");

            // Step 2: Alter the UserId column type
            migrationBuilder.AlterColumn<string>(
                name: "UserId",
                table: "TradeHistories",
                type: "nvarchar(450)",
                nullable: false,
                oldClrType: typeof(long),
                oldType: "bigint");

            // Step 3: Recreate the primary key constraint
            migrationBuilder.AddPrimaryKey(
                name: "PK_TradeHistories",
                table: "TradeHistories",
                columns: new[] { "UserId", "TradeId" });

            migrationBuilder.AlterColumn<DateTime>(
                name: "CreatedAt",
                table: "TradeHistories",
                type: "datetime2",
                nullable: false,
                defaultValue: new DateTime(2025, 12, 12, 11, 21, 45, 813, DateTimeKind.Utc).AddTicks(6920),
                oldClrType: typeof(DateTime),
                oldType: "datetime2",
                oldDefaultValue: new DateTime(2025, 12, 11, 6, 0, 46, 979, DateTimeKind.Utc).AddTicks(6120));
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            // Step 1: Drop the primary key constraint
            migrationBuilder.DropPrimaryKey(
                name: "PK_TradeHistories",
                table: "TradeHistories");

            // Step 2: Alter the UserId column type back
            migrationBuilder.AlterColumn<long>(
                name: "UserId",
                table: "TradeHistories",
                type: "bigint",
                nullable: false,
                oldClrType: typeof(string),
                oldType: "nvarchar(450)");

            // Step 3: Recreate the primary key constraint
            migrationBuilder.AddPrimaryKey(
                name: "PK_TradeHistories",
                table: "TradeHistories",
                columns: new[] { "UserId", "TradeId" });

            migrationBuilder.AlterColumn<DateTime>(
                name: "CreatedAt",
                table: "TradeHistories",
                type: "datetime2",
                nullable: false,
                defaultValue: new DateTime(2025, 12, 11, 6, 0, 46, 979, DateTimeKind.Utc).AddTicks(6120),
                oldClrType: typeof(DateTime),
                oldType: "datetime2",
                oldDefaultValue: new DateTime(2025, 12, 12, 11, 21, 45, 813, DateTimeKind.Utc).AddTicks(6920));
        }
    }
}
