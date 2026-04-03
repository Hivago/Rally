using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace RallyAPI.Catalog.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class AddOptionGroupsAndItemTags : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            // 1. Create menu_item_option_groups table
            migrationBuilder.CreateTable(
                name: "menu_item_option_groups",
                schema: "catalog",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    menu_item_id = table.Column<Guid>(type: "uuid", nullable: false),
                    group_name = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: false),
                    is_required = table.Column<bool>(type: "boolean", nullable: false),
                    min_selections = table.Column<int>(type: "integer", nullable: false),
                    max_selections = table.Column<int>(type: "integer", nullable: false),
                    display_order = table.Column<int>(type: "integer", nullable: false),
                    created_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    updated_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    DeletedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_menu_item_option_groups", x => x.id);
                    table.ForeignKey(
                        name: "FK_menu_item_option_groups_menu_items_menu_item_id",
                        column: x => x.menu_item_id,
                        principalSchema: "catalog",
                        principalTable: "menu_items",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "ix_menu_item_option_groups_menu_item_id",
                schema: "catalog",
                table: "menu_item_option_groups",
                column: "menu_item_id");

            // 2. Add option_group_id FK column to menu_item_options
            migrationBuilder.AddColumn<Guid>(
                name: "option_group_id",
                schema: "catalog",
                table: "menu_item_options",
                type: "uuid",
                nullable: true);

            migrationBuilder.CreateIndex(
                name: "ix_menu_item_options_option_group_id",
                schema: "catalog",
                table: "menu_item_options",
                column: "option_group_id");

            migrationBuilder.AddForeignKey(
                name: "FK_menu_item_options_menu_item_option_groups_option_group_id",
                schema: "catalog",
                table: "menu_item_options",
                column: "option_group_id",
                principalSchema: "catalog",
                principalTable: "menu_item_option_groups",
                principalColumn: "id",
                onDelete: ReferentialAction.Cascade);

            // 3. Add tags column to menu_items (PostgreSQL text array)
            migrationBuilder.AddColumn<string[]>(
                name: "tags",
                schema: "catalog",
                table: "menu_items",
                type: "text[]",
                nullable: false,
                defaultValueSql: "'{}'");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "tags",
                schema: "catalog",
                table: "menu_items");

            migrationBuilder.DropForeignKey(
                name: "FK_menu_item_options_menu_item_option_groups_option_group_id",
                schema: "catalog",
                table: "menu_item_options");

            migrationBuilder.DropIndex(
                name: "ix_menu_item_options_option_group_id",
                schema: "catalog",
                table: "menu_item_options");

            migrationBuilder.DropColumn(
                name: "option_group_id",
                schema: "catalog",
                table: "menu_item_options");

            migrationBuilder.DropTable(
                name: "menu_item_option_groups",
                schema: "catalog");
        }
    }
}
