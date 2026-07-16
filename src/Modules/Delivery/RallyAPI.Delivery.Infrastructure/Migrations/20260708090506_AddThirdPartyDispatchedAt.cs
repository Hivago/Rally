using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace RallyAPI.Delivery.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class AddThirdPartyDispatchedAt : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<DateTime>(
                name: "third_party_dispatched_at",
                schema: "delivery",
                table: "delivery_requests",
                type: "timestamp with time zone",
                nullable: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "third_party_dispatched_at",
                schema: "delivery",
                table: "delivery_requests");
        }
    }
}
