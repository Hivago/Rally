using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace RallyAPI.Orders.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class AddOrderEscalationFields : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<DateTime>(
                name: "escalated_at",
                schema: "orders",
                table: "orders",
                type: "timestamp with time zone",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "escalation_reason",
                schema: "orders",
                table: "orders",
                type: "character varying(500)",
                maxLength: 500,
                nullable: true);

            migrationBuilder.AddColumn<bool>(
                name: "is_escalated",
                schema: "orders",
                table: "orders",
                type: "boolean",
                nullable: false,
                defaultValue: false);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "escalated_at",
                schema: "orders",
                table: "orders");

            migrationBuilder.DropColumn(
                name: "escalation_reason",
                schema: "orders",
                table: "orders");

            migrationBuilder.DropColumn(
                name: "is_escalated",
                schema: "orders",
                table: "orders");
        }
    }
}
