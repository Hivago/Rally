using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace RallyAPI.Users.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class AddCommissionFlatFee : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<decimal>(
                name: "commission_flat_fee",
                schema: "users",
                table: "restaurants",
                type: "numeric(10,2)",
                precision: 10,
                scale: 2,
                nullable: false,
                defaultValue: 30.00m);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "commission_flat_fee",
                schema: "users",
                table: "restaurants");
        }
    }
}
