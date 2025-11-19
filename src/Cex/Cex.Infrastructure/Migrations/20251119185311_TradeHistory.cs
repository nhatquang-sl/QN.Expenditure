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
            migrationBuilder.CreateTable(
                name: "TradeHistories",
                columns: table => new
                {
                    UserId = table.Column<long>(type: "bigint", nullable: false),
                    TradeId = table.Column<long>(type: "bigint", nullable: false),
                    Symbol = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    OrderId = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    CounterOrderId = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    Side = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    Liquidity = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    ForceTaker = table.Column<bool>(type: "bit", nullable: false),
                    Price = table.Column<decimal>(type: "decimal(13,6)", precision: 13, scale: 6, nullable: false),
                    Size = table.Column<decimal>(type: "decimal(13,6)", precision: 13, scale: 6, nullable: false),
                    Funds = table.Column<decimal>(type: "decimal(13,6)", precision: 13, scale: 6, nullable: false),
                    Fee = table.Column<decimal>(type: "decimal(13,6)", precision: 13, scale: 6, nullable: false),
                    FeeRate = table.Column<decimal>(type: "decimal(13,6)", precision: 13, scale: 6, nullable: false),
                    FeeCurrency = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    Stop = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    TradeType = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    Type = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    TradedAt = table.Column<DateTime>(type: "datetime2", nullable: false),
                    CreatedAt = table.Column<DateTime>(type: "datetime2", nullable: false, defaultValue: new DateTime(2025, 11, 19, 18, 53, 10, 803, DateTimeKind.Utc).AddTicks(5040))
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_TradeHistories", x => new { x.UserId, x.TradeId });
                });
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "TradeHistories");
        }
    }
}
