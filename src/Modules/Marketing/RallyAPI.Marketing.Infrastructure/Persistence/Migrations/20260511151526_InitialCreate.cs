using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace RallyAPI.Marketing.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class InitialCreate : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.EnsureSchema(
                name: "marketing");

            migrationBuilder.CreateTable(
                name: "customer_waitlist",
                schema: "marketing",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    Name = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: false),
                    Email = table.Column<string>(type: "character varying(320)", maxLength: 320, nullable: false),
                    Phone = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: false),
                    Source = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: true),
                    IpAddress = table.Column<string>(type: "character varying(45)", maxLength: 45, nullable: true),
                    CreatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    UpdatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    DeletedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_customer_waitlist", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "restaurant_leads",
                schema: "marketing",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    RestaurantName = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: false),
                    OwnerName = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: false),
                    Phone = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: false),
                    City = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: false),
                    DailyOrders = table.Column<int>(type: "integer", nullable: false),
                    Source = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: true),
                    IpAddress = table.Column<string>(type: "character varying(45)", maxLength: 45, nullable: true),
                    CreatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    UpdatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    DeletedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_restaurant_leads", x => x.Id);
                });

            migrationBuilder.CreateIndex(
                name: "IX_customer_waitlist_CreatedAt",
                schema: "marketing",
                table: "customer_waitlist",
                column: "CreatedAt");

            migrationBuilder.CreateIndex(
                name: "IX_customer_waitlist_Email",
                schema: "marketing",
                table: "customer_waitlist",
                column: "Email",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_customer_waitlist_Phone",
                schema: "marketing",
                table: "customer_waitlist",
                column: "Phone",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_restaurant_leads_City",
                schema: "marketing",
                table: "restaurant_leads",
                column: "City");

            migrationBuilder.CreateIndex(
                name: "IX_restaurant_leads_CreatedAt",
                schema: "marketing",
                table: "restaurant_leads",
                column: "CreatedAt");

            migrationBuilder.CreateIndex(
                name: "IX_restaurant_leads_Phone",
                schema: "marketing",
                table: "restaurant_leads",
                column: "Phone",
                unique: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "customer_waitlist",
                schema: "marketing");

            migrationBuilder.DropTable(
                name: "restaurant_leads",
                schema: "marketing");
        }
    }
}
