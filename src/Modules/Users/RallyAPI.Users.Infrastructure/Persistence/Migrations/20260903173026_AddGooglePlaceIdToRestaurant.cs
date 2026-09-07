using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace RallyAPI.Users.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class AddGooglePlaceIdToRestaurant : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "google_place_id",
                schema: "users",
                table: "restaurants",
                type: "character varying(255)",
                maxLength: 255,
                nullable: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "google_place_id",
                schema: "users",
                table: "restaurants");
        }
    }
}
