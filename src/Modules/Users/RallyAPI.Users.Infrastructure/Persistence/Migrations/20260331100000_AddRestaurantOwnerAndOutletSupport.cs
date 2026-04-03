using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace RallyAPI.Users.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class AddRestaurantOwnerAndOutletSupport : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            // 1. Create restaurant_owners table
            migrationBuilder.CreateTable(
                name: "restaurant_owners",
                schema: "users",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    name = table.Column<string>(type: "character varying(255)", maxLength: 255, nullable: false),
                    email = table.Column<string>(type: "character varying(255)", maxLength: 255, nullable: false),
                    password_hash = table.Column<string>(type: "character varying(255)", maxLength: 255, nullable: false),
                    phone = table.Column<string>(type: "character varying(15)", maxLength: 15, nullable: false),
                    pan_number = table.Column<string>(type: "character varying(10)", maxLength: 10, nullable: true),
                    gst_number = table.Column<string>(type: "character varying(15)", maxLength: 15, nullable: true),
                    bank_account_number = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: true),
                    bank_ifsc_code = table.Column<string>(type: "character varying(11)", maxLength: 11, nullable: true),
                    bank_account_name = table.Column<string>(type: "character varying(255)", maxLength: 255, nullable: true),
                    is_active = table.Column<bool>(type: "boolean", nullable: false, defaultValue: true),
                    created_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    updated_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    deleted_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    version = table.Column<int>(type: "integer", nullable: false, defaultValue: 1)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_restaurant_owners", x => x.id);
                });

            migrationBuilder.CreateIndex(
                name: "idx_restaurant_owners_email",
                schema: "users",
                table: "restaurant_owners",
                column: "email",
                unique: true);

            // 2. Add new columns to restaurants table
            migrationBuilder.AddColumn<Guid>(
                name: "owner_id",
                schema: "users",
                table: "restaurants",
                type: "uuid",
                nullable: true); // nullable initially for data migration

            migrationBuilder.AddColumn<string>(
                name: "fssai_number",
                schema: "users",
                table: "restaurants",
                type: "character varying(20)",
                maxLength: 20,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "cuisine_types",
                schema: "users",
                table: "restaurants",
                type: "jsonb",
                nullable: false,
                defaultValueSql: "'[]'::jsonb");

            migrationBuilder.AddColumn<bool>(
                name: "is_pure_veg",
                schema: "users",
                table: "restaurants",
                type: "boolean",
                nullable: false,
                defaultValue: false);

            migrationBuilder.AddColumn<bool>(
                name: "is_vegan_friendly",
                schema: "users",
                table: "restaurants",
                type: "boolean",
                nullable: false,
                defaultValue: false);

            migrationBuilder.AddColumn<bool>(
                name: "has_jain_options",
                schema: "users",
                table: "restaurants",
                type: "boolean",
                nullable: false,
                defaultValue: false);

            migrationBuilder.AddColumn<decimal>(
                name: "min_order_amount",
                schema: "users",
                table: "restaurants",
                type: "numeric(10,2)",
                precision: 10,
                scale: 2,
                nullable: false,
                defaultValue: 0m);

            // 3. Data migration: create an owner record for each existing restaurant
            migrationBuilder.Sql(@"
                INSERT INTO users.restaurant_owners (id, name, email, password_hash, phone, is_active, created_at, updated_at, version)
                SELECT gen_random_uuid(), name, email, password_hash, phone, is_active, created_at, updated_at, 1
                FROM users.restaurants
                WHERE deleted_at IS NULL;
            ");

            // 4. Link restaurants to their new owner records
            migrationBuilder.Sql(@"
                UPDATE users.restaurants r
                SET owner_id = o.id
                FROM users.restaurant_owners o
                WHERE o.email = r.email;
            ");

            // 5. Add FK constraint (owner_id is now populated for all existing rows)
            migrationBuilder.CreateIndex(
                name: "ix_restaurants_owner_id",
                schema: "users",
                table: "restaurants",
                column: "owner_id");

            migrationBuilder.AddForeignKey(
                name: "FK_restaurants_restaurant_owners_owner_id",
                schema: "users",
                table: "restaurants",
                column: "owner_id",
                principalSchema: "users",
                principalTable: "restaurant_owners",
                principalColumn: "id",
                onDelete: ReferentialAction.Restrict);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_restaurants_restaurant_owners_owner_id",
                schema: "users",
                table: "restaurants");

            migrationBuilder.DropIndex(
                name: "ix_restaurants_owner_id",
                schema: "users",
                table: "restaurants");

            migrationBuilder.DropColumn(name: "owner_id", schema: "users", table: "restaurants");
            migrationBuilder.DropColumn(name: "fssai_number", schema: "users", table: "restaurants");
            migrationBuilder.DropColumn(name: "cuisine_types", schema: "users", table: "restaurants");
            migrationBuilder.DropColumn(name: "is_pure_veg", schema: "users", table: "restaurants");
            migrationBuilder.DropColumn(name: "is_vegan_friendly", schema: "users", table: "restaurants");
            migrationBuilder.DropColumn(name: "has_jain_options", schema: "users", table: "restaurants");
            migrationBuilder.DropColumn(name: "min_order_amount", schema: "users", table: "restaurants");

            migrationBuilder.DropTable(
                name: "restaurant_owners",
                schema: "users");
        }
    }
}
