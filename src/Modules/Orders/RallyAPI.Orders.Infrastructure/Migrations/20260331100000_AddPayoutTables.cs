using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace RallyAPI.Orders.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class AddPayoutTables : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            // 1. Create payouts table
            migrationBuilder.CreateTable(
                name: "payouts",
                schema: "orders",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    owner_id = table.Column<Guid>(type: "uuid", nullable: false),
                    period_start = table.Column<DateOnly>(type: "date", nullable: false),
                    period_end = table.Column<DateOnly>(type: "date", nullable: false),
                    order_count = table.Column<int>(type: "integer", nullable: false),
                    gross_order_amount = table.Column<decimal>(type: "numeric(10,2)", precision: 10, scale: 2, nullable: false),
                    total_gst_collected = table.Column<decimal>(type: "numeric(10,2)", precision: 10, scale: 2, nullable: false),
                    total_commission = table.Column<decimal>(type: "numeric(10,2)", precision: 10, scale: 2, nullable: false),
                    total_commission_gst = table.Column<decimal>(type: "numeric(10,2)", precision: 10, scale: 2, nullable: false),
                    total_tds = table.Column<decimal>(type: "numeric(10,2)", precision: 10, scale: 2, nullable: false),
                    net_payout_amount = table.Column<decimal>(type: "numeric(10,2)", precision: 10, scale: 2, nullable: false),
                    status = table.Column<int>(type: "integer", nullable: false),
                    bank_account_number = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: true),
                    bank_ifsc_code = table.Column<string>(type: "character varying(11)", maxLength: 11, nullable: true),
                    transaction_reference = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: true),
                    paid_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    notes = table.Column<string>(type: "character varying(2000)", maxLength: 2000, nullable: true),
                    created_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    updated_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_payouts", x => x.id);
                });

            migrationBuilder.CreateIndex(
                name: "ix_payouts_owner_id",
                schema: "orders",
                table: "payouts",
                column: "owner_id");

            migrationBuilder.CreateIndex(
                name: "ix_payouts_owner_period",
                schema: "orders",
                table: "payouts",
                columns: new[] { "owner_id", "period_start", "period_end" });

            migrationBuilder.CreateIndex(
                name: "ix_payouts_status",
                schema: "orders",
                table: "payouts",
                column: "status");

            // 2. Create payout_ledger table
            migrationBuilder.CreateTable(
                name: "payout_ledger",
                schema: "orders",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    owner_id = table.Column<Guid>(type: "uuid", nullable: false),
                    outlet_id = table.Column<Guid>(type: "uuid", nullable: false),
                    order_id = table.Column<Guid>(type: "uuid", nullable: false),
                    order_amount = table.Column<decimal>(type: "numeric(10,2)", precision: 10, scale: 2, nullable: false),
                    gst_amount = table.Column<decimal>(type: "numeric(10,2)", precision: 10, scale: 2, nullable: false),
                    commission_percentage = table.Column<decimal>(type: "numeric(5,2)", precision: 5, scale: 2, nullable: false),
                    commission_amount = table.Column<decimal>(type: "numeric(10,2)", precision: 10, scale: 2, nullable: false),
                    commission_gst = table.Column<decimal>(type: "numeric(10,2)", precision: 10, scale: 2, nullable: false),
                    tds_amount = table.Column<decimal>(type: "numeric(10,2)", precision: 10, scale: 2, nullable: false),
                    net_amount = table.Column<decimal>(type: "numeric(10,2)", precision: 10, scale: 2, nullable: false),
                    currency = table.Column<string>(type: "character varying(3)", maxLength: 3, nullable: false, defaultValue: "INR"),
                    status = table.Column<int>(type: "integer", nullable: false),
                    payout_id = table.Column<Guid>(type: "uuid", nullable: true),
                    created_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    updated_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_payout_ledger", x => x.id);
                    table.ForeignKey(
                        name: "FK_payout_ledger_payouts_payout_id",
                        column: x => x.payout_id,
                        principalSchema: "orders",
                        principalTable: "payouts",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateIndex(
                name: "ix_payout_ledger_owner_id",
                schema: "orders",
                table: "payout_ledger",
                column: "owner_id");

            migrationBuilder.CreateIndex(
                name: "ix_payout_ledger_outlet_id",
                schema: "orders",
                table: "payout_ledger",
                column: "outlet_id");

            migrationBuilder.CreateIndex(
                name: "ix_payout_ledger_order_id",
                schema: "orders",
                table: "payout_ledger",
                column: "order_id",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "ix_payout_ledger_payout_id",
                schema: "orders",
                table: "payout_ledger",
                column: "payout_id");

            migrationBuilder.CreateIndex(
                name: "ix_payout_ledger_owner_status",
                schema: "orders",
                table: "payout_ledger",
                columns: new[] { "owner_id", "status" });
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "payout_ledger",
                schema: "orders");

            migrationBuilder.DropTable(
                name: "payouts",
                schema: "orders");
        }
    }
}
