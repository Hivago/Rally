using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace RallyAPI.Orders.Infrastructure.Migrations
{
    public partial class AddFulfillmentTypeToOrders : Migration
    {
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            // Add fulfillment_type column (0 = Delivery, 10 = Pickup)
            // Default to 0 (Delivery) so all existing orders remain delivery orders
            migrationBuilder.AddColumn<int>(
                name: "fulfillment_type",
                schema: "orders",
                table: "orders",
                type: "integer",
                nullable: false,
                defaultValue: 0);

            // Make delivery_info FK nullable (pickup orders have no DeliveryInfo)
            migrationBuilder.Sql(
                @"ALTER TABLE orders.delivery_info ALTER COLUMN order_id DROP NOT NULL;");
        }

        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "fulfillment_type",
                schema: "orders",
                table: "orders");

            migrationBuilder.Sql(
                @"ALTER TABLE orders.delivery_info ALTER COLUMN order_id SET NOT NULL;");
        }
    }
}
