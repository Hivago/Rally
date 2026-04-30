using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace RallyAPI.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class InitialAuditDb : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.EnsureSchema(
                name: "audit");

            migrationBuilder.CreateTable(
                name: "webhook_audit_logs",
                schema: "audit",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    Source = table.Column<string>(type: "character varying(50)", maxLength: 50, nullable: false),
                    EventId = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: false),
                    ReceivedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    SourceIp = table.Column<string>(type: "character varying(45)", maxLength: 45, nullable: false),
                    SignatureValid = table.Column<bool>(type: "boolean", nullable: false),
                    TimestampValid = table.Column<bool>(type: "boolean", nullable: false),
                    IsDuplicate = table.Column<bool>(type: "boolean", nullable: false),
                    ProcessingStatus = table.Column<string>(type: "character varying(50)", maxLength: 50, nullable: false),
                    RawBody = table.Column<string>(type: "text", nullable: false),
                    ErrorMessage = table.Column<string>(type: "text", nullable: true),
                    CorrelationId = table.Column<Guid>(type: "uuid", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_webhook_audit_logs", x => x.Id);
                });
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "webhook_audit_logs",
                schema: "audit");
        }
    }
}
