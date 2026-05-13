using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace RallyAPI.Users.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class AddRestaurantAcceptsPickup : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<bool>(
                name: "accepts_pickup",
                schema: "users",
                table: "restaurants",
                type: "boolean",
                nullable: false,
                defaultValue: false);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "accepts_pickup",
                schema: "users",
                table: "restaurants");
        }
    }
}
