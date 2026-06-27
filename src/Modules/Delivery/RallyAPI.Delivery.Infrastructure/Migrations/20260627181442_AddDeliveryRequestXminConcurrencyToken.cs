using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace RallyAPI.Delivery.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class AddDeliveryRequestXminConcurrencyToken : Migration
    {
        // No-op DDL by design. `xmin` is a Postgres SYSTEM column that exists on every
        // table automatically — it cannot be added or dropped. EF's model differ doesn't
        // know that and generated an AddColumn/DropColumn here; running it would throw
        // 'column name "xmin" conflicts with a system column name' and crash startup.
        // We keep the migration (so the model snapshot records xmin as the concurrency
        // token) but emit no SQL. UseXminAsConcurrencyToken() in DeliveryRequestConfiguration
        // is what actually wires the optimistic-concurrency check.

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
