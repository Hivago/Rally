using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace RallyAPI.Orders.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class AddPayoutExportTracking : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<Guid>(
                name: "export_batch_id",
                schema: "orders",
                table: "payouts",
                type: "uuid",
                nullable: true);

            migrationBuilder.AddColumn<DateTime>(
                name: "exported_at_utc",
                schema: "orders",
                table: "payouts",
                type: "timestamp with time zone",
                nullable: true);

            migrationBuilder.CreateIndex(
                name: "ix_payouts_export_batch_id",
                schema: "orders",
                table: "payouts",
                column: "export_batch_id");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "ix_payouts_export_batch_id",
                schema: "orders",
                table: "payouts");

            migrationBuilder.DropColumn(
                name: "export_batch_id",
                schema: "orders",
                table: "payouts");

            migrationBuilder.DropColumn(
                name: "exported_at_utc",
                schema: "orders",
                table: "payouts");
        }
    }
}
