using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace RallyAPI.Users.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class AddRiderPayoutConcurrency : Migration
    {
        // No-op DDL by design. `xmin` is a Postgres SYSTEM column that exists on every
        // table automatically — it cannot be added or dropped. EF's model differ doesn't
        // know that and generated an AddColumn/DropColumn here; running it would throw
        // 'column name "xmin" conflicts with a system column name' and crash startup.
        // We keep the migration (so the model snapshot records xmin as the concurrency
        // token) but emit no SQL. UseXminAsConcurrencyToken() in RiderPayoutLedgerConfiguration
        // is what actually wires the optimistic-concurrency check. Same fix as
        // RallyAPI.Delivery.Infrastructure.Migrations.AddDeliveryRequestXminConcurrencyToken
        // (commit b78e764) and Orders' AddPayoutConcurrencyAndUniquePeriod.

        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
        }
    }
}
