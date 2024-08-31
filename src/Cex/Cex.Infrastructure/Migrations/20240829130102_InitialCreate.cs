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
                name: "Candles",
                columns: table => new
                {
                    Session = table.Column<long>(type: "bigint", nullable: false),
                    OpenPrice = table.Column<decimal>(type: "decimal(8,3)", precision: 8, scale: 3, nullable: false),
                    ClosePrice = table.Column<decimal>(type: "decimal(8,3)", precision: 8, scale: 3, nullable: false),
                    HighPrice = table.Column<decimal>(type: "decimal(8,3)", precision: 8, scale: 3, nullable: false),
                    LowPrice = table.Column<decimal>(type: "decimal(8,3)", precision: 8, scale: 3, nullable: false),
                    BaseVolume = table.Column<decimal>(type: "decimal(8,3)", precision: 8, scale: 3, nullable: false),
                    QuoteVolume = table.Column<decimal>(type: "decimal(13,3)", precision: 13, scale: 3, nullable: false),
                    IsBetSession = table.Column<bool>(type: "bit", nullable: false),
                    OpenDateTime = table.Column<DateTime>(type: "datetime2", nullable: false),
                    CloseDateTime = table.Column<DateTime>(type: "datetime2", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Candles", x => x.Session);
                });

            migrationBuilder.CreateTable(
                name: "Configs",
                columns: table => new
                {
                    Key = table.Column<string>(type: "nvarchar(450)", nullable: false),
                    Value = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    UpdatedAt = table.Column<DateTime>(type: "datetime2", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Configs", x => x.Key);
                });
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "Candles");

            migrationBuilder.DropTable(
                name: "Configs");
        }
    }
}
