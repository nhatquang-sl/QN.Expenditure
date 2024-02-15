using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class SpotOrders : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "SpotOrders",
                columns: table => new
                {
                    OrderId = table.Column<long>(type: "bigint", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    Symbol = table.Column<string>(type: "nvarchar(10)", maxLength: 10, nullable: false),
                    OrderListId = table.Column<int>(type: "int", nullable: false),
                    ClientOrderId = table.Column<string>(type: "nvarchar(255)", maxLength: 255, nullable: false),
                    Price = table.Column<decimal>(type: "decimal(10,8)", precision: 10, scale: 8, nullable: false),
                    OrigQty = table.Column<decimal>(type: "decimal(10,8)", precision: 10, scale: 8, nullable: false),
                    ExecutedQty = table.Column<decimal>(type: "decimal(10,8)", precision: 10, scale: 8, nullable: false),
                    CummulativeQuoteQty = table.Column<decimal>(type: "decimal(10,8)", precision: 10, scale: 8, nullable: false),
                    Status = table.Column<string>(type: "nvarchar(10)", maxLength: 10, nullable: false),
                    TimeInForce = table.Column<string>(type: "nvarchar(10)", maxLength: 10, nullable: false),
                    Type = table.Column<string>(type: "nvarchar(10)", maxLength: 10, nullable: false),
                    Side = table.Column<string>(type: "nvarchar(10)", maxLength: 10, nullable: false),
                    StopPrice = table.Column<decimal>(type: "decimal(10,8)", precision: 10, scale: 8, nullable: false),
                    IcebergQty = table.Column<decimal>(type: "decimal(10,8)", precision: 10, scale: 8, nullable: false),
                    Time = table.Column<DateTime>(type: "datetime2", nullable: false),
                    UpdateTime = table.Column<DateTime>(type: "datetime2", nullable: false),
                    IsWorking = table.Column<bool>(type: "bit", nullable: false),
                    WorkingTime = table.Column<DateTime>(type: "datetime2", nullable: false),
                    OrigQuoteOrderQty = table.Column<decimal>(type: "decimal(10,8)", precision: 10, scale: 8, nullable: false),
                    SelfTradePreventionMode = table.Column<string>(type: "nvarchar(10)", maxLength: 10, nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_SpotOrders", x => x.OrderId);
                });

            migrationBuilder.CreateTable(
                name: "SpotOrderSyncSettings",
                columns: table => new
                {
                    UserId = table.Column<string>(type: "nvarchar(36)", maxLength: 36, nullable: false),
                    Symbol = table.Column<string>(type: "nvarchar(10)", maxLength: 10, nullable: false),
                    LastSyncAt = table.Column<DateTime>(type: "datetime2", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_SpotOrderSyncSettings", x => new { x.Symbol, x.UserId });
                });
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "SpotOrders");

            migrationBuilder.DropTable(
                name: "SpotOrderSyncSettings");
        }
    }
}
