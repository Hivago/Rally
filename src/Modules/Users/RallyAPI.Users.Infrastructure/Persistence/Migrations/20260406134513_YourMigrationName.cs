using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace RallyAPI.Users.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class YourMigrationName : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.RenameIndex(
                name: "ix_restaurants_owner_id",
                schema: "users",
                table: "restaurants",
                newName: "IX_restaurants_owner_id");

            migrationBuilder.AlterColumn<int>(
                name: "version",
                schema: "users",
                table: "restaurant_owners",
                type: "integer",
                nullable: false,
                oldClrType: typeof(int),
                oldType: "integer",
                oldDefaultValue: 1);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.RenameIndex(
                name: "IX_restaurants_owner_id",
                schema: "users",
                table: "restaurants",
                newName: "ix_restaurants_owner_id");

            migrationBuilder.AlterColumn<int>(
                name: "version",
                schema: "users",
                table: "restaurant_owners",
                type: "integer",
                nullable: false,
                defaultValue: 1,
                oldClrType: typeof(int),
                oldType: "integer");
        }
    }
}
