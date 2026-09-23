using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace RallyAPI.Delivery.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class AddRestaurantPinDriftTelemetry : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<double>(
                name: "arrived_pickup_latitude",
                schema: "delivery",
                table: "delivery_requests",
                type: "double precision",
                nullable: true);

            migrationBuilder.AddColumn<double>(
                name: "arrived_pickup_longitude",
                schema: "delivery",
                table: "delivery_requests",
                type: "double precision",
                nullable: true);

            migrationBuilder.AddColumn<decimal>(
                name: "pickup_drift_meters",
                schema: "delivery",
                table: "delivery_requests",
                type: "numeric(8,2)",
                precision: 8,
                scale: 2,
                nullable: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "arrived_pickup_latitude",
                schema: "delivery",
                table: "delivery_requests");

            migrationBuilder.DropColumn(
                name: "arrived_pickup_longitude",
                schema: "delivery",
                table: "delivery_requests");

            migrationBuilder.DropColumn(
                name: "pickup_drift_meters",
                schema: "delivery",
                table: "delivery_requests");
        }
    }
}
