using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace RallyAPI.Marketing.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class AddRestaurantOnboardingApplication : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "restaurant_onboarding_applications",
                schema: "marketing",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    RestaurantName = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: false),
                    OwnerName = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: false),
                    Phone = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: false),
                    Email = table.Column<string>(type: "character varying(255)", maxLength: 255, nullable: false),
                    City = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: false),
                    AddressLine = table.Column<string>(type: "character varying(500)", maxLength: 500, nullable: false),
                    CuisineType = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: true),
                    FssaiNumber = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: true),
                    BankAccountNumberEncrypted = table.Column<string>(type: "text", nullable: false),
                    BankAccountLast4 = table.Column<string>(type: "character varying(4)", maxLength: 4, nullable: false),
                    BankIfscCode = table.Column<string>(type: "character varying(11)", maxLength: 11, nullable: false),
                    BankAccountName = table.Column<string>(type: "character varying(255)", maxLength: 255, nullable: false),
                    PanNumberEncrypted = table.Column<string>(type: "text", nullable: false),
                    PanLast4 = table.Column<string>(type: "character varying(4)", maxLength: 4, nullable: false),
                    GstNumberEncrypted = table.Column<string>(type: "text", nullable: true),
                    GstLast4 = table.Column<string>(type: "character varying(4)", maxLength: 4, nullable: true),
                    Status = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: false),
                    ReviewedByAdminId = table.Column<Guid>(type: "uuid", nullable: true),
                    ReviewedAtUtc = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    ReviewNotes = table.Column<string>(type: "character varying(2000)", maxLength: 2000, nullable: true),
                    Source = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: true),
                    IpAddress = table.Column<string>(type: "character varying(45)", maxLength: 45, nullable: true),
                    CreatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    UpdatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    DeletedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_restaurant_onboarding_applications", x => x.Id);
                });

            migrationBuilder.CreateIndex(
                name: "IX_restaurant_onboarding_applications_CreatedAt",
                schema: "marketing",
                table: "restaurant_onboarding_applications",
                column: "CreatedAt");

            migrationBuilder.CreateIndex(
                name: "IX_restaurant_onboarding_applications_Email",
                schema: "marketing",
                table: "restaurant_onboarding_applications",
                column: "Email");

            migrationBuilder.CreateIndex(
                name: "IX_restaurant_onboarding_applications_Phone",
                schema: "marketing",
                table: "restaurant_onboarding_applications",
                column: "Phone");

            migrationBuilder.CreateIndex(
                name: "IX_restaurant_onboarding_applications_Status",
                schema: "marketing",
                table: "restaurant_onboarding_applications",
                column: "Status");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "restaurant_onboarding_applications",
                schema: "marketing");
        }
    }
}
