using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Cex.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class AddSignalDurationColumns : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<int>(
                name: "EntryHitAfterMinutes",
                table: "Signals",
                type: "int",
                nullable: false,
                defaultValue: -1);

            migrationBuilder.AddColumn<int>(
                name: "MaxProfitHitAfterMinutes",
                table: "Signals",
                type: "int",
                nullable: false,
                defaultValue: -1);

            migrationBuilder.AddColumn<int>(
                name: "StopLossHitAfterMinutes",
                table: "Signals",
                type: "int",
                nullable: false,
                defaultValue: -1);

            migrationBuilder.Sql(@"
                UPDATE Signals SET EntryHitAfterMinutes = ISNULL(DATEDIFF(MINUTE, DetectedAt, EntryHitAt), -1);
                UPDATE Signals SET MaxProfitHitAfterMinutes = ISNULL(DATEDIFF(MINUTE, DetectedAt, MaxProfitHitAt), -1);
                UPDATE Signals SET StopLossHitAfterMinutes = ISNULL(DATEDIFF(MINUTE, DetectedAt, StopLossHitAt), -1);
            ");

            migrationBuilder.CreateIndex(
                name: "IX_Signals_EntryHitAfterMinutes",
                table: "Signals",
                column: "EntryHitAfterMinutes");

            migrationBuilder.CreateIndex(
                name: "IX_Signals_MaxProfitHitAfterMinutes",
                table: "Signals",
                column: "MaxProfitHitAfterMinutes");

            migrationBuilder.CreateIndex(
                name: "IX_Signals_StopLossHitAfterMinutes",
                table: "Signals",
                column: "StopLossHitAfterMinutes");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "IX_Signals_EntryHitAfterMinutes",
                table: "Signals");

            migrationBuilder.DropIndex(
                name: "IX_Signals_MaxProfitHitAfterMinutes",
                table: "Signals");

            migrationBuilder.DropIndex(
                name: "IX_Signals_StopLossHitAfterMinutes",
                table: "Signals");

            migrationBuilder.DropColumn(
                name: "EntryHitAfterMinutes",
                table: "Signals");

            migrationBuilder.DropColumn(
                name: "MaxProfitHitAfterMinutes",
                table: "Signals");

            migrationBuilder.DropColumn(
                name: "StopLossHitAfterMinutes",
                table: "Signals");
        }
    }
}
