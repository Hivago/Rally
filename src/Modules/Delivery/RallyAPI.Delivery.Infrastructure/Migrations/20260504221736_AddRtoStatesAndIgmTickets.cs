using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace RallyAPI.Delivery.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class AddRtoStatesAndIgmTickets : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "drop_code",
                schema: "delivery",
                table: "delivery_requests",
                type: "character varying(10)",
                maxLength: 10,
                nullable: true);

            migrationBuilder.AddColumn<DateTime>(
                name: "last_location_updated_at",
                schema: "delivery",
                table: "delivery_requests",
                type: "timestamp with time zone",
                nullable: true);

            migrationBuilder.AddColumn<double>(
                name: "last_rider_latitude",
                schema: "delivery",
                table: "delivery_requests",
                type: "double precision",
                nullable: true);

            migrationBuilder.AddColumn<double>(
                name: "last_rider_longitude",
                schema: "delivery",
                table: "delivery_requests",
                type: "double precision",
                nullable: true);

            migrationBuilder.AddColumn<int>(
                name: "order_category",
                schema: "delivery",
                table: "delivery_requests",
                type: "integer",
                nullable: false,
                defaultValue: 1);

            migrationBuilder.AddColumn<string>(
                name: "pickup_code",
                schema: "delivery",
                table: "delivery_requests",
                type: "character varying(10)",
                maxLength: 10,
                nullable: true);

            migrationBuilder.AddColumn<DateTime>(
                name: "rto_delivered_at",
                schema: "delivery",
                table: "delivery_requests",
                type: "timestamp with time zone",
                nullable: true);

            migrationBuilder.AddColumn<DateTime>(
                name: "rto_disposed_at",
                schema: "delivery",
                table: "delivery_requests",
                type: "timestamp with time zone",
                nullable: true);

            migrationBuilder.AddColumn<DateTime>(
                name: "rto_initiated_at",
                schema: "delivery",
                table: "delivery_requests",
                type: "timestamp with time zone",
                nullable: true);

            migrationBuilder.CreateTable(
                name: "igm_tickets",
                schema: "delivery",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    delivery_request_id = table.Column<Guid>(type: "uuid", nullable: false),
                    order_id = table.Column<Guid>(type: "uuid", nullable: false),
                    order_number = table.Column<string>(type: "character varying(50)", maxLength: 50, nullable: false),
                    issue_type = table.Column<int>(type: "integer", nullable: false),
                    category = table.Column<string>(type: "character varying(50)", maxLength: 50, nullable: false),
                    sub_category = table.Column<string>(type: "character varying(50)", maxLength: 50, nullable: false),
                    description_short = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: false),
                    description_long = table.Column<string>(type: "character varying(2000)", maxLength: 2000, nullable: true),
                    state = table.Column<int>(type: "integer", nullable: false),
                    external_issue_id = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: true),
                    resolution_action = table.Column<int>(type: "integer", nullable: true),
                    resolution_short_desc = table.Column<string>(type: "character varying(500)", maxLength: 500, nullable: true),
                    resolution_long_desc = table.Column<string>(type: "character varying(2000)", maxLength: 2000, nullable: true),
                    refund_amount = table.Column<decimal>(type: "numeric(18,2)", precision: 18, scale: 2, nullable: true),
                    rating = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: true),
                    refund_by_lsp = table.Column<bool>(type: "boolean", nullable: true),
                    refund_to_client = table.Column<bool>(type: "boolean", nullable: true),
                    raised_by_admin_id = table.Column<Guid>(type: "uuid", nullable: false),
                    pushed_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    resolved_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    closed_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    created_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    updated_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    deleted_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    version = table.Column<int>(type: "integer", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_igm_tickets", x => x.id);
                });

            migrationBuilder.CreateIndex(
                name: "ix_igm_tickets_delivery_request_id",
                schema: "delivery",
                table: "igm_tickets",
                column: "delivery_request_id");

            migrationBuilder.CreateIndex(
                name: "ix_igm_tickets_external_issue_id",
                schema: "delivery",
                table: "igm_tickets",
                column: "external_issue_id");

            migrationBuilder.CreateIndex(
                name: "ix_igm_tickets_order_id",
                schema: "delivery",
                table: "igm_tickets",
                column: "order_id");

            migrationBuilder.CreateIndex(
                name: "ix_igm_tickets_state",
                schema: "delivery",
                table: "igm_tickets",
                column: "state");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "igm_tickets",
                schema: "delivery");

            migrationBuilder.DropColumn(
                name: "drop_code",
                schema: "delivery",
                table: "delivery_requests");

            migrationBuilder.DropColumn(
                name: "last_location_updated_at",
                schema: "delivery",
                table: "delivery_requests");

            migrationBuilder.DropColumn(
                name: "last_rider_latitude",
                schema: "delivery",
                table: "delivery_requests");

            migrationBuilder.DropColumn(
                name: "last_rider_longitude",
                schema: "delivery",
                table: "delivery_requests");

            migrationBuilder.DropColumn(
                name: "order_category",
                schema: "delivery",
                table: "delivery_requests");

            migrationBuilder.DropColumn(
                name: "pickup_code",
                schema: "delivery",
                table: "delivery_requests");

            migrationBuilder.DropColumn(
                name: "rto_delivered_at",
                schema: "delivery",
                table: "delivery_requests");

            migrationBuilder.DropColumn(
                name: "rto_disposed_at",
                schema: "delivery",
                table: "delivery_requests");

            migrationBuilder.DropColumn(
                name: "rto_initiated_at",
                schema: "delivery",
                table: "delivery_requests");
        }
    }
}
