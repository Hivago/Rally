using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace RallyAPI.Users.Infrastructure.Persistence.Migration
{
    /// <inheritdoc />
    public partial class AddRiderDeliveryTrackingNew : Microsoft.EntityFrameworkCore.Migrations.Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "idx_riders_location",
                schema: "users",
                table: "riders");

            migrationBuilder.AddColumn<DateTime>(
                name: "current_delivery_assigned_at",
                schema: "users",
                table: "riders",
                type: "timestamp with time zone",
                nullable: true);

            migrationBuilder.AddColumn<Guid>(
                name: "current_delivery_id",
                schema: "users",
                table: "riders",
                type: "uuid",
                nullable: true);

            migrationBuilder.CreateIndex(
                name: "ix_riders_availability",
                schema: "users",
                table: "riders",
                columns: new[] { "is_online", "is_active", "current_delivery_id" });

            migrationBuilder.CreateIndex(
                name: "ix_riders_location",
                schema: "users",
                table: "riders",
                columns: new[] { "current_latitude", "current_longitude" },
                filter: "current_latitude IS NOT NULL AND current_longitude IS NOT NULL");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "ix_riders_availability",
                schema: "users",
                table: "riders");

            migrationBuilder.DropIndex(
                name: "ix_riders_location",
                schema: "users",
                table: "riders");

            migrationBuilder.DropColumn(
                name: "current_delivery_assigned_at",
                schema: "users",
                table: "riders");

            migrationBuilder.DropColumn(
                name: "current_delivery_id",
                schema: "users",
                table: "riders");

            migrationBuilder.CreateIndex(
                name: "idx_riders_location",
                schema: "users",
                table: "riders",
                columns: new[] { "current_latitude", "current_longitude" });
        }
    }
}
