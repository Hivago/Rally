using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace RallyAPI.Orders.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class AddPayoutConcurrencyAndUniquePeriod : Migration
    {
        // `xmin` is a Postgres SYSTEM column that exists on every table automatically — it
        // cannot be added or dropped. EF's model differ doesn't know that and scaffolded an
        // AddColumn/DropColumn for it here; running that would throw 'column name "xmin"
        // conflicts with a system column name' and crash startup. Stripped, following the
        // same fix as RallyAPI.Delivery.Infrastructure.Migrations
        // .AddDeliveryRequestXminConcurrencyToken (commit b78e764). The real index change
        // (unique owner+period) is untouched below.

        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "ix_payouts_owner_period",
                schema: "orders",
                table: "payouts");

            migrationBuilder.CreateIndex(
                name: "ix_payouts_owner_period",
                schema: "orders",
                table: "payouts",
                columns: new[] { "owner_id", "period_start", "period_end" },
                unique: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "ix_payouts_owner_period",
                schema: "orders",
                table: "payouts");

            migrationBuilder.CreateIndex(
                name: "ix_payouts_owner_period",
                schema: "orders",
                table: "payouts",
                columns: new[] { "owner_id", "period_start", "period_end" });
        }
    }
}
