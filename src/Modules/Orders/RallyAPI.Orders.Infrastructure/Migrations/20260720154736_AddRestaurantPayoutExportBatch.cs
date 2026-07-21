using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace RallyAPI.Orders.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class AddRestaurantPayoutExportBatch : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "restaurant_payout_export_batches",
                schema: "orders",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    period_start = table.Column<DateOnly>(type: "date", nullable: false),
                    period_end = table.Column<DateOnly>(type: "date", nullable: false),
                    row_count = table.Column<int>(type: "integer", nullable: false),
                    control_sum_total = table.Column<decimal>(type: "numeric(12,2)", precision: 12, scale: 2, nullable: false),
                    status = table.Column<int>(type: "integer", nullable: false),
                    generated_by_admin_id = table.Column<Guid>(type: "uuid", nullable: false),
                    generated_at_utc = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    generated_file_hash = table.Column<string>(type: "character varying(64)", maxLength: 64, nullable: false),
                    reconciled_by_admin_id = table.Column<Guid>(type: "uuid", nullable: true),
                    reconciled_at_utc = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    reconciliation_file_hash = table.Column<string>(type: "character varying(64)", maxLength: 64, nullable: true),
                    created_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    updated_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_restaurant_payout_export_batches", x => x.id);
                });

            migrationBuilder.CreateIndex(
                name: "ix_restaurant_payout_export_batches_period",
                schema: "orders",
                table: "restaurant_payout_export_batches",
                columns: new[] { "period_start", "period_end" });

            migrationBuilder.CreateIndex(
                name: "ix_restaurant_payout_export_batches_status",
                schema: "orders",
                table: "restaurant_payout_export_batches",
                column: "status");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "restaurant_payout_export_batches",
                schema: "orders");
        }
    }
}
