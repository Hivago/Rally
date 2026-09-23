using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace RallyAPI.Users.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class AddRestaurantLocationDriftReview : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<DateTimeOffset>(
                name: "location_drift_detected_at",
                schema: "users",
                table: "restaurants",
                type: "timestamp with time zone",
                nullable: true);

            migrationBuilder.AddColumn<int>(
                name: "location_status",
                schema: "users",
                table: "restaurants",
                type: "integer",
                nullable: false,
                defaultValue: 1);

            migrationBuilder.CreateTable(
                name: "restaurant_location_review_queue",
                schema: "users",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    restaurant_id = table.Column<Guid>(type: "uuid", nullable: false),
                    current_latitude = table.Column<decimal>(type: "numeric(9,6)", precision: 9, scale: 6, nullable: false),
                    current_longitude = table.Column<decimal>(type: "numeric(9,6)", precision: 9, scale: 6, nullable: false),
                    suggested_latitude = table.Column<decimal>(type: "numeric(9,6)", precision: 9, scale: 6, nullable: false),
                    suggested_longitude = table.Column<decimal>(type: "numeric(9,6)", precision: 9, scale: 6, nullable: false),
                    average_drift_meters = table.Column<decimal>(type: "numeric(8,2)", precision: 8, scale: 2, nullable: false),
                    sample_size = table.Column<int>(type: "integer", nullable: false),
                    status = table.Column<int>(type: "integer", nullable: false, defaultValue: 1),
                    detected_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    reviewed_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    reviewed_by_admin_id = table.Column<Guid>(type: "uuid", nullable: true),
                    created_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    updated_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    deleted_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_restaurant_location_review_queue", x => x.id);
                    table.ForeignKey(
                        name: "FK_restaurant_location_review_queue_restaurants_restaurant_id",
                        column: x => x.restaurant_id,
                        principalSchema: "users",
                        principalTable: "restaurants",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "ix_restaurant_location_review_queue_restaurant_status",
                schema: "users",
                table: "restaurant_location_review_queue",
                columns: new[] { "restaurant_id", "status" });

            migrationBuilder.CreateIndex(
                name: "ix_restaurant_location_review_queue_status",
                schema: "users",
                table: "restaurant_location_review_queue",
                column: "status");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "restaurant_location_review_queue",
                schema: "users");

            migrationBuilder.DropColumn(
                name: "location_drift_detected_at",
                schema: "users",
                table: "restaurants");

            migrationBuilder.DropColumn(
                name: "location_status",
                schema: "users",
                table: "restaurants");
        }
    }
}
