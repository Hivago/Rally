using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace RallyAPI.Users.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class AddRiderPayoutLedger : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "rider_payout_ledger",
                schema: "users",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    rider_id = table.Column<Guid>(type: "uuid", nullable: false),
                    cycle_start = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    cycle_end = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    delivery_count = table.Column<int>(type: "integer", nullable: false, defaultValue: 0),
                    base_fare = table.Column<decimal>(type: "numeric(10,2)", precision: 10, scale: 2, nullable: false, defaultValue: 0m),
                    surge_fare = table.Column<decimal>(type: "numeric(10,2)", precision: 10, scale: 2, nullable: false, defaultValue: 0m),
                    tips = table.Column<decimal>(type: "numeric(10,2)", precision: 10, scale: 2, nullable: false, defaultValue: 0m),
                    net_payable = table.Column<decimal>(type: "numeric(10,2)", precision: 10, scale: 2, nullable: false),
                    status = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: false, defaultValue: "Pending"),
                    status_note = table.Column<string>(type: "character varying(500)", maxLength: 500, nullable: true),
                    paid_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    failure_reason = table.Column<string>(type: "character varying(500)", maxLength: 500, nullable: true),
                    transaction_reference = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: true),
                    created_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    updated_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_rider_payout_ledger", x => x.id);
                    table.ForeignKey(
                        name: "FK_rider_payout_ledger_riders_rider_id",
                        column: x => x.rider_id,
                        principalSchema: "users",
                        principalTable: "riders",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateIndex(
                name: "idx_rider_payout_ledger_rider_id",
                schema: "users",
                table: "rider_payout_ledger",
                column: "rider_id");

            migrationBuilder.CreateIndex(
                name: "idx_rider_payout_ledger_cycle",
                schema: "users",
                table: "rider_payout_ledger",
                columns: new[] { "cycle_start", "cycle_end", "status" });

            migrationBuilder.CreateIndex(
                name: "idx_rider_payout_ledger_rider_cycle",
                schema: "users",
                table: "rider_payout_ledger",
                columns: new[] { "rider_id", "cycle_start", "cycle_end" },
                unique: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "rider_payout_ledger",
                schema: "users");
        }
    }
}
