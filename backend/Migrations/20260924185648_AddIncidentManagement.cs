using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace backend.Migrations
{
    /// <inheritdoc />
    public partial class AddIncidentManagement : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "incidents",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    incident_number = table.Column<string>(type: "character varying(50)", maxLength: 50, nullable: false),
                    title = table.Column<string>(type: "character varying(300)", maxLength: 300, nullable: false),
                    description = table.Column<string>(type: "text", nullable: true),
                    source = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: false),
                    source_type = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: false),
                    source_id = table.Column<Guid>(type: "uuid", nullable: true),
                    target = table.Column<string>(type: "character varying(500)", maxLength: 500, nullable: false),
                    target_type = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: false),
                    severity = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: false),
                    status = table.Column<string>(type: "character varying(30)", maxLength: 30, nullable: false),
                    failure_type = table.Column<string>(type: "character varying(50)", maxLength: 50, nullable: false),
                    http_status_code = table.Column<int>(type: "integer", nullable: true),
                    latency_ms = table.Column<double>(type: "double precision", nullable: true),
                    error_message = table.Column<string>(type: "text", nullable: true),
                    detected_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    acknowledged_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    resolved_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    assigned_agent = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: true),
                    root_cause = table.Column<string>(type: "text", nullable: true),
                    recommended_action = table.Column<string>(type: "text", nullable: true),
                    resolution = table.Column<string>(type: "text", nullable: true),
                    requires_approval = table.Column<bool>(type: "boolean", nullable: false),
                    remediation_attempted = table.Column<bool>(type: "boolean", nullable: false),
                    remediation_successful = table.Column<bool>(type: "boolean", nullable: false),
                    created_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    updated_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_incidents", x => x.id);
                });

            migrationBuilder.CreateIndex(
                name: "IX_incidents_detected_at",
                table: "incidents",
                column: "detected_at");

            migrationBuilder.CreateIndex(
                name: "IX_incidents_incident_number",
                table: "incidents",
                column: "incident_number",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_incidents_severity",
                table: "incidents",
                column: "severity");

            migrationBuilder.CreateIndex(
                name: "IX_incidents_source_id_status",
                table: "incidents",
                columns: new[] { "source_id", "status" });
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "incidents");
        }
    }
}
