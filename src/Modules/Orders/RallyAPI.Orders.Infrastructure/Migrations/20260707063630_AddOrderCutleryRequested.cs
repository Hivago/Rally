using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace RallyAPI.Orders.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class AddOrderCutleryRequested : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            // NOTE: EF also wanted to DROP phantom "DeletedAt"/"Version" columns from
            // orders.payouts / orders.payout_ledger — those columns are .Ignore()'d in
            // config and were never created in the DB, so the stale snapshot listed them
            // erroneously. Dropping them would crash startup. Those ops are intentionally
            // omitted; this migration only adds the new cutlery column, and the corrected
            // snapshot heals the phantom drift going forward.
            migrationBuilder.AddColumn<bool>(
                name: "cutlery_requested",
                schema: "orders",
                table: "orders",
                type: "boolean",
                nullable: false,
                defaultValue: false);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "cutlery_requested",
                schema: "orders",
                table: "orders");
        }
    }
}
