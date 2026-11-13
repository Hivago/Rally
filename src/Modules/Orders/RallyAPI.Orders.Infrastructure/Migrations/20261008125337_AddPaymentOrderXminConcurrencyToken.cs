using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace RallyAPI.Orders.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class AddPaymentOrderXminConcurrencyToken : Migration
    {
        // `xmin` is a Postgres SYSTEM column present on every table; it cannot be added or
        // dropped. EF scaffolded AddColumn/DropColumn for it, which would throw 'column name
        // "xmin" conflicts with a system column name' and crash startup. Stripped, same as
        // AddPayoutConcurrencyAndUniquePeriod. This migration only updates the model snapshot.

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
