using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace RallyAPI.Users.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class AddRiderPayoutExportTracking : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<Guid>(
                name: "export_batch_id",
                schema: "users",
                table: "rider_payout_ledger",
                type: "uuid",
                nullable: true);

            migrationBuilder.AddColumn<DateTime>(
                name: "exported_at_utc",
                schema: "users",
                table: "rider_payout_ledger",
                type: "timestamp with time zone",
                nullable: true);

            migrationBuilder.CreateIndex(
                name: "idx_rider_payout_ledger_export_batch_id",
                schema: "users",
                table: "rider_payout_ledger",
                column: "export_batch_id");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "idx_rider_payout_ledger_export_batch_id",
                schema: "users",
                table: "rider_payout_ledger");

            migrationBuilder.DropColumn(
                name: "export_batch_id",
                schema: "users",
                table: "rider_payout_ledger");

            migrationBuilder.DropColumn(
                name: "exported_at_utc",
                schema: "users",
                table: "rider_payout_ledger");
        }
    }
}
