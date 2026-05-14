using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace RallyAPI.Delivery.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class AddProRoutingProviderCost : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<decimal>(
                name: "provider_distance_km",
                schema: "delivery",
                table: "delivery_requests",
                type: "numeric(8,2)",
                precision: 8,
                scale: 2,
                nullable: true);

            migrationBuilder.AddColumn<decimal>(
                name: "provider_lsp_fee",
                schema: "delivery",
                table: "delivery_requests",
                type: "numeric(10,2)",
                precision: 10,
                scale: 2,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "provider_network_order_id",
                schema: "delivery",
                table: "delivery_requests",
                type: "character varying(100)",
                maxLength: 100,
                nullable: true);

            migrationBuilder.AddColumn<decimal>(
                name: "provider_platform_fee",
                schema: "delivery",
                table: "delivery_requests",
                type: "numeric(10,2)",
                precision: 10,
                scale: 2,
                nullable: true);

            migrationBuilder.AddColumn<decimal>(
                name: "provider_total_with_tax",
                schema: "delivery",
                table: "delivery_requests",
                type: "numeric(10,2)",
                precision: 10,
                scale: 2,
                nullable: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "provider_distance_km",
                schema: "delivery",
                table: "delivery_requests");

            migrationBuilder.DropColumn(
                name: "provider_lsp_fee",
                schema: "delivery",
                table: "delivery_requests");

            migrationBuilder.DropColumn(
                name: "provider_network_order_id",
                schema: "delivery",
                table: "delivery_requests");

            migrationBuilder.DropColumn(
                name: "provider_platform_fee",
                schema: "delivery",
                table: "delivery_requests");

            migrationBuilder.DropColumn(
                name: "provider_total_with_tax",
                schema: "delivery",
                table: "delivery_requests");
        }
    }
}
