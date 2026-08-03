using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace RallyAPI.Orders.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class AddOrderNumberToPayoutLedger : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "order_number",
                schema: "orders",
                table: "payout_ledger",
                type: "character varying(50)",
                maxLength: 50,
                nullable: false,
                defaultValue: "");

            // Backfill existing rows from orders.orders — order_id is a unique FK on
            // payout_ledger, so every historical row can recover its human-readable
            // order number instead of being stuck with the "" default above.
            migrationBuilder.Sql(@"
                UPDATE orders.payout_ledger AS pl
                SET order_number = o.order_number
                FROM orders.orders AS o
                WHERE pl.order_id = o.id;");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "order_number",
                schema: "orders",
                table: "payout_ledger");
        }
    }
}
