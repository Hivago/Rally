using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace RallyAPI.Delivery.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class InitialDelivery : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.EnsureSchema(
                name: "delivery");

            migrationBuilder.CreateTable(
                name: "delivery_requests",
                schema: "delivery",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    order_id = table.Column<Guid>(type: "uuid", nullable: false),
                    order_number = table.Column<string>(type: "character varying(50)", maxLength: 50, nullable: false),
                    quote_id = table.Column<Guid>(type: "uuid", nullable: true),
                    restaurant_id = table.Column<Guid>(type: "uuid", nullable: true),
                    customer_id = table.Column<Guid>(type: "uuid", nullable: true),
                    item_count = table.Column<int>(type: "integer", nullable: true),
                    total_amount = table.Column<decimal>(type: "numeric(10,2)", precision: 10, scale: 2, nullable: true),
                    delivery_instructions = table.Column<string>(type: "character varying(500)", maxLength: 500, nullable: true),
                    status = table.Column<int>(type: "integer", nullable: false),
                    fleet_type = table.Column<int>(type: "integer", nullable: true),
                    quoted_price = table.Column<decimal>(type: "numeric(10,2)", precision: 10, scale: 2, nullable: false),
                    actual_price = table.Column<decimal>(type: "numeric(10,2)", precision: 10, scale: 2, nullable: true),
                    price_difference = table.Column<decimal>(type: "numeric(10,2)", precision: 10, scale: 2, nullable: true),
                    rider_id = table.Column<Guid>(type: "uuid", nullable: true),
                    rider_name = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: true),
                    rider_phone = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: true),
                    external_task_id = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: true),
                    external_tracking_url = table.Column<string>(type: "character varying(500)", maxLength: 500, nullable: true),
                    external_rider_name = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: true),
                    external_rider_phone = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: true),
                    external_lsp_name = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: true),
                    pickup_latitude = table.Column<double>(type: "double precision", nullable: false),
                    pickup_longitude = table.Column<double>(type: "double precision", nullable: false),
                    pickup_pincode = table.Column<string>(type: "character varying(10)", maxLength: 10, nullable: false),
                    pickup_address = table.Column<string>(type: "character varying(500)", maxLength: 500, nullable: false),
                    pickup_contact_name = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: false),
                    pickup_contact_phone = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: false),
                    drop_latitude = table.Column<double>(type: "double precision", nullable: false),
                    drop_longitude = table.Column<double>(type: "double precision", nullable: false),
                    drop_pincode = table.Column<string>(type: "character varying(10)", maxLength: 10, nullable: false),
                    drop_address = table.Column<string>(type: "character varying(500)", maxLength: 500, nullable: false),
                    drop_contact_name = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: false),
                    drop_contact_phone = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: false),
                    created_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    dispatch_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    searching_started_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    assigned_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    arrived_pickup_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    picked_up_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    arrived_drop_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    delivered_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    failed_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    cancelled_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    updated_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    failure_reason = table.Column<int>(type: "integer", nullable: true),
                    failure_notes = table.Column<string>(type: "character varying(500)", maxLength: 500, nullable: true),
                    failure_photo_url = table.Column<string>(type: "character varying(500)", maxLength: 500, nullable: true),
                    own_fleet_attempts = table.Column<int>(type: "integer", nullable: false, defaultValue: 0),
                    distance_km = table.Column<decimal>(type: "numeric(8,2)", precision: 8, scale: 2, nullable: true),
                    estimated_minutes = table.Column<int>(type: "integer", nullable: true),
                    DeletedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    Version = table.Column<int>(type: "integer", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_delivery_requests", x => x.id);
                });

            migrationBuilder.CreateTable(
                name: "quotes",
                schema: "delivery",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    pickup_latitude = table.Column<double>(type: "double precision", nullable: false),
                    pickup_longitude = table.Column<double>(type: "double precision", nullable: false),
                    pickup_pincode = table.Column<string>(type: "character varying(10)", maxLength: 10, nullable: false),
                    drop_latitude = table.Column<double>(type: "double precision", nullable: false),
                    drop_longitude = table.Column<double>(type: "double precision", nullable: false),
                    drop_pincode = table.Column<string>(type: "character varying(10)", maxLength: 10, nullable: false),
                    city = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: false),
                    order_amount = table.Column<decimal>(type: "numeric(10,2)", precision: 10, scale: 2, nullable: false),
                    restaurant_id = table.Column<Guid>(type: "uuid", nullable: true),
                    distance_km = table.Column<decimal>(type: "numeric(8,2)", precision: 8, scale: 2, nullable: false),
                    base_fee = table.Column<decimal>(type: "numeric(10,2)", precision: 10, scale: 2, nullable: false),
                    final_fee = table.Column<decimal>(type: "numeric(10,2)", precision: 10, scale: 2, nullable: false),
                    surge_multiplier = table.Column<decimal>(type: "numeric(4,2)", precision: 4, scale: 2, nullable: false, defaultValue: 1.0m),
                    surge_reason = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: true),
                    estimated_minutes = table.Column<int>(type: "integer", nullable: false),
                    fleet_type = table.Column<int>(type: "integer", nullable: false),
                    provider_name = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: false),
                    provider_quote_id = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: true),
                    created_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    expires_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    is_used = table.Column<bool>(type: "boolean", nullable: false, defaultValue: false),
                    used_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    used_for_order_id = table.Column<Guid>(type: "uuid", nullable: true),
                    breakdown = table.Column<string>(type: "jsonb", nullable: true),
                    UpdatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    DeletedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_quotes", x => x.id);
                });

            migrationBuilder.CreateTable(
                name: "rider_offers",
                schema: "delivery",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    delivery_request_id = table.Column<Guid>(type: "uuid", nullable: false),
                    rider_id = table.Column<Guid>(type: "uuid", nullable: false),
                    offered_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    expires_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    earnings = table.Column<decimal>(type: "numeric(10,2)", precision: 10, scale: 2, nullable: false),
                    status = table.Column<int>(type: "integer", nullable: false),
                    responded_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    rejection_reason = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: true),
                    rider_latitude = table.Column<double>(type: "double precision", nullable: true),
                    rider_longitude = table.Column<double>(type: "double precision", nullable: true),
                    distance_to_restaurant_km = table.Column<decimal>(type: "numeric(8,2)", precision: 8, scale: 2, nullable: true),
                    notification_sent_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    notification_delivered = table.Column<bool>(type: "boolean", nullable: false, defaultValue: false),
                    CreatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    UpdatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    DeletedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_rider_offers", x => x.id);
                    table.ForeignKey(
                        name: "FK_rider_offers_delivery_requests_delivery_request_id",
                        column: x => x.delivery_request_id,
                        principalSchema: "delivery",
                        principalTable: "delivery_requests",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "ix_delivery_requests_dispatch_at",
                schema: "delivery",
                table: "delivery_requests",
                column: "dispatch_at");

            migrationBuilder.CreateIndex(
                name: "ix_delivery_requests_external_task_id",
                schema: "delivery",
                table: "delivery_requests",
                column: "external_task_id");

            migrationBuilder.CreateIndex(
                name: "ix_delivery_requests_order_id",
                schema: "delivery",
                table: "delivery_requests",
                column: "order_id",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "ix_delivery_requests_rider_id",
                schema: "delivery",
                table: "delivery_requests",
                column: "rider_id");

            migrationBuilder.CreateIndex(
                name: "ix_delivery_requests_status",
                schema: "delivery",
                table: "delivery_requests",
                column: "status");

            migrationBuilder.CreateIndex(
                name: "ix_quotes_expires_at",
                schema: "delivery",
                table: "quotes",
                column: "expires_at");

            migrationBuilder.CreateIndex(
                name: "ix_quotes_is_used",
                schema: "delivery",
                table: "quotes",
                column: "is_used");

            migrationBuilder.CreateIndex(
                name: "ix_quotes_order_id",
                schema: "delivery",
                table: "quotes",
                column: "used_for_order_id");

            migrationBuilder.CreateIndex(
                name: "ix_rider_offers_delivery_request_id",
                schema: "delivery",
                table: "rider_offers",
                column: "delivery_request_id");

            migrationBuilder.CreateIndex(
                name: "ix_rider_offers_rider_id",
                schema: "delivery",
                table: "rider_offers",
                column: "rider_id");

            migrationBuilder.CreateIndex(
                name: "ix_rider_offers_rider_status",
                schema: "delivery",
                table: "rider_offers",
                columns: new[] { "rider_id", "status" });

            migrationBuilder.CreateIndex(
                name: "ix_rider_offers_status",
                schema: "delivery",
                table: "rider_offers",
                column: "status");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "quotes",
                schema: "delivery");

            migrationBuilder.DropTable(
                name: "rider_offers",
                schema: "delivery");

            migrationBuilder.DropTable(
                name: "delivery_requests",
                schema: "delivery");
        }
    }
}
