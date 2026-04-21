using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace RallyAPI.Users.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class AddRestaurantSettingsAndSchedule : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<int>(
                name: "delivery_mode",
                schema: "users",
                table: "restaurants",
                type: "integer",
                nullable: false,
                defaultValue: 0);

            migrationBuilder.AddColumn<string>(
                name: "description",
                schema: "users",
                table: "restaurants",
                type: "character varying(2000)",
                maxLength: 2000,
                nullable: true);

            migrationBuilder.AddColumn<int>(
                name: "dietary_type",
                schema: "users",
                table: "restaurants",
                type: "integer",
                nullable: false,
                defaultValue: 2);

            migrationBuilder.AddColumn<bool>(
                name: "notify_browser",
                schema: "users",
                table: "restaurants",
                type: "boolean",
                nullable: false,
                defaultValue: true);

            migrationBuilder.AddColumn<bool>(
                name: "notify_email_alerts",
                schema: "users",
                table: "restaurants",
                type: "boolean",
                nullable: false,
                defaultValue: true);

            migrationBuilder.AddColumn<bool>(
                name: "notify_order_sound",
                schema: "users",
                table: "restaurants",
                type: "boolean",
                nullable: false,
                defaultValue: true);

            migrationBuilder.AddColumn<bool>(
                name: "use_custom_schedule",
                schema: "users",
                table: "restaurants",
                type: "boolean",
                nullable: false,
                defaultValue: false);

            migrationBuilder.CreateTable(
                name: "restaurant_schedule_slots",
                schema: "users",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    restaurant_id = table.Column<Guid>(type: "uuid", nullable: false),
                    day_of_week = table.Column<int>(type: "integer", nullable: false),
                    opens_at = table.Column<TimeOnly>(type: "time without time zone", nullable: false),
                    closes_at = table.Column<TimeOnly>(type: "time without time zone", nullable: false),
                    created_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    updated_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    deleted_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_restaurant_schedule_slots", x => x.id);
                    table.ForeignKey(
                        name: "FK_restaurant_schedule_slots_restaurants_restaurant_id",
                        column: x => x.restaurant_id,
                        principalSchema: "users",
                        principalTable: "restaurants",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "idx_restaurant_schedule_slots_restaurant_day",
                schema: "users",
                table: "restaurant_schedule_slots",
                columns: new[] { "restaurant_id", "day_of_week" });
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "restaurant_schedule_slots",
                schema: "users");

            migrationBuilder.DropColumn(
                name: "delivery_mode",
                schema: "users",
                table: "restaurants");

            migrationBuilder.DropColumn(
                name: "description",
                schema: "users",
                table: "restaurants");

            migrationBuilder.DropColumn(
                name: "dietary_type",
                schema: "users",
                table: "restaurants");

            migrationBuilder.DropColumn(
                name: "notify_browser",
                schema: "users",
                table: "restaurants");

            migrationBuilder.DropColumn(
                name: "notify_email_alerts",
                schema: "users",
                table: "restaurants");

            migrationBuilder.DropColumn(
                name: "notify_order_sound",
                schema: "users",
                table: "restaurants");

            migrationBuilder.DropColumn(
                name: "use_custom_schedule",
                schema: "users",
                table: "restaurants");
        }
    }
}
