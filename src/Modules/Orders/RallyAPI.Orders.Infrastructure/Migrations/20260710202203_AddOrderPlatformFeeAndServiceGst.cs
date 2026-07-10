using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace RallyAPI.Orders.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class AddOrderPlatformFeeAndServiceGst : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<decimal>(
                name: "platform_fee",
                schema: "orders",
                table: "orders",
                type: "numeric(10,2)",
                precision: 10,
                scale: 2,
                nullable: false,
                defaultValue: 0m);

            migrationBuilder.AddColumn<string>(
                name: "platform_fee_currency",
                schema: "orders",
                table: "orders",
                type: "character varying(3)",
                maxLength: 3,
                nullable: false,
                defaultValue: "INR");

            migrationBuilder.AddColumn<decimal>(
                name: "service_gst",
                schema: "orders",
                table: "orders",
                type: "numeric(10,2)",
                precision: 10,
                scale: 2,
                nullable: false,
                defaultValue: 0m);

            migrationBuilder.AddColumn<string>(
                name: "service_gst_currency",
                schema: "orders",
                table: "orders",
                type: "character varying(3)",
                maxLength: 3,
                nullable: false,
                defaultValue: "INR");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "platform_fee",
                schema: "orders",
                table: "orders");

            migrationBuilder.DropColumn(
                name: "platform_fee_currency",
                schema: "orders",
                table: "orders");

            migrationBuilder.DropColumn(
                name: "service_gst",
                schema: "orders",
                table: "orders");

            migrationBuilder.DropColumn(
                name: "service_gst_currency",
                schema: "orders",
                table: "orders");
        }
    }
}
