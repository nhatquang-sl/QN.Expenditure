using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Cex.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class InitialCreate : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "BnbSettings",
                columns: table => new
                {
                    UserId = table.Column<string>(type: "nvarchar(450)", nullable: false),
                    ApiKey = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    SecretKey = table.Column<string>(type: "nvarchar(max)", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_BnbSettings", x => x.UserId);
                });

            migrationBuilder.CreateTable(
                name: "ExchangeConfigs",
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
                    table.PrimaryKey("PK_ExchangeConfigs", x => new { x.UserId, x.ExchangeName });
                });

            migrationBuilder.CreateTable(
                name: "SpotGrids",
                columns: table => new
                {
                    Id = table.Column<long>(type: "bigint", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    UserId = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    Symbol = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    LowerPrice = table.Column<decimal>(type: "decimal(13,6)", precision: 13, scale: 6, nullable: false),
                    UpperPrice = table.Column<decimal>(type: "decimal(13,6)", precision: 13, scale: 6, nullable: false),
                    TriggerPrice = table.Column<decimal>(type: "decimal(13,6)", precision: 13, scale: 6, nullable: false),
                    NumberOfGrids = table.Column<int>(type: "int", nullable: false),
                    GridMode = table.Column<int>(type: "int", nullable: false),
                    Investment = table.Column<decimal>(type: "decimal(13,6)", precision: 13, scale: 6, nullable: false),
                    BaseBalance = table.Column<decimal>(type: "decimal(13,6)", precision: 13, scale: 6, nullable: false),
                    QuoteBalance = table.Column<decimal>(type: "decimal(13,6)", precision: 13, scale: 6, nullable: false),
                    Profit = table.Column<decimal>(type: "decimal(13,6)", precision: 13, scale: 6, nullable: false),
                    TakeProfit = table.Column<decimal>(type: "decimal(13,6)", precision: 13, scale: 6, nullable: true),
                    StopLoss = table.Column<decimal>(type: "decimal(13,6)", precision: 13, scale: 6, nullable: true),
                    Status = table.Column<int>(type: "int", nullable: false),
                    CreatedAt = table.Column<DateTime>(type: "datetime2", nullable: false),
                    DeletedAt = table.Column<DateTime>(type: "datetime2", nullable: true),
                    UpdatedAt = table.Column<DateTime>(type: "datetime2", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_SpotGrids", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "SpotOrderSyncSettings",
                columns: table => new
                {
                    UserId = table.Column<string>(type: "nvarchar(450)", nullable: false),
                    Symbol = table.Column<string>(type: "nvarchar(450)", nullable: false),
                    LastSyncAt = table.Column<DateTime>(type: "datetime2", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_SpotOrderSyncSettings", x => new { x.UserId, x.Symbol });
                });

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
                    CreatedAt = table.Column<DateTime>(type: "datetime2", nullable: false, defaultValue: new DateTime(2025, 12, 2, 11, 34, 44, 602, DateTimeKind.Utc).AddTicks(4460))
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_TradeHistories", x => new { x.UserId, x.TradeId });
                });

            migrationBuilder.CreateTable(
                name: "SpotGridSteps",
                columns: table => new
                {
                    Id = table.Column<long>(type: "bigint", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    SpotGridId = table.Column<long>(type: "bigint", nullable: false),
                    BuyPrice = table.Column<decimal>(type: "decimal(13,6)", precision: 13, scale: 6, nullable: false),
                    SellPrice = table.Column<decimal>(type: "decimal(13,6)", precision: 13, scale: 6, nullable: false),
                    Qty = table.Column<decimal>(type: "decimal(13,6)", precision: 13, scale: 6, nullable: false),
                    OrderId = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    Status = table.Column<string>(type: "nvarchar(16)", maxLength: 16, nullable: false),
                    Type = table.Column<string>(type: "nvarchar(16)", maxLength: 16, nullable: false),
                    DeletedAt = table.Column<DateTime>(type: "datetime2", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_SpotGridSteps", x => x.Id);
                    table.ForeignKey(
                        name: "FK_SpotGridSteps_SpotGrids_SpotGridId",
                        column: x => x.SpotGridId,
                        principalTable: "SpotGrids",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "SpotOrders",
                columns: table => new
                {
                    OrderId = table.Column<string>(type: "nvarchar(450)", nullable: false),
                    UserId = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    Symbol = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    ClientOrderId = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    Price = table.Column<decimal>(type: "decimal(13,6)", precision: 13, scale: 6, nullable: false),
                    OrigQty = table.Column<decimal>(type: "decimal(18,6)", precision: 18, scale: 6, nullable: false),
                    TimeInForce = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    Type = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    Side = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    Fee = table.Column<decimal>(type: "decimal(18,10)", precision: 18, scale: 10, nullable: false),
                    FeeCurrency = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    CreatedAt = table.Column<DateTime>(type: "datetime2", nullable: false),
                    UpdatedAt = table.Column<DateTime>(type: "datetime2", nullable: false),
                    IsWorking = table.Column<bool>(type: "bit", nullable: false),
                    WorkingTime = table.Column<DateTime>(type: "datetime2", nullable: false),
                    SpotGridStepId = table.Column<long>(type: "bigint", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_SpotOrders", x => x.OrderId);
                    table.ForeignKey(
                        name: "FK_SpotOrders_SpotGridSteps_SpotGridStepId",
                        column: x => x.SpotGridStepId,
                        principalTable: "SpotGridSteps",
                        principalColumn: "Id");
                });

            migrationBuilder.CreateIndex(
                name: "IX_ExchangeConfigs_UserId",
                table: "ExchangeConfigs",
                column: "UserId");

            migrationBuilder.CreateIndex(
                name: "IX_SpotGridSteps_SpotGridId",
                table: "SpotGridSteps",
                column: "SpotGridId");

            migrationBuilder.CreateIndex(
                name: "IX_SpotOrders_SpotGridStepId",
                table: "SpotOrders",
                column: "SpotGridStepId");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "BnbSettings");

            migrationBuilder.DropTable(
                name: "ExchangeConfigs");

            migrationBuilder.DropTable(
                name: "SpotOrders");

            migrationBuilder.DropTable(
                name: "SpotOrderSyncSettings");

            migrationBuilder.DropTable(
                name: "TradeHistories");

            migrationBuilder.DropTable(
                name: "SpotGridSteps");

            migrationBuilder.DropTable(
                name: "SpotGrids");
        }
    }
}
