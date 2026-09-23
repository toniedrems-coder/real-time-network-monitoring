using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace backend.Migrations
{
    /// <inheritdoc />
    public partial class InitialMonitoringDatabase : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "detected_anomalies",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    endpoint_id = table.Column<Guid>(type: "uuid", nullable: false),
                    type = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: false),
                    severity = table.Column<string>(type: "character varying(50)", maxLength: 50, nullable: false),
                    description = table.Column<string>(type: "character varying(1000)", maxLength: 1000, nullable: false),
                    detected_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    observed_value = table.Column<double>(type: "double precision", nullable: false),
                    expected_value = table.Column<double>(type: "double precision", nullable: false),
                    metric_id = table.Column<Guid>(type: "uuid", nullable: false),
                    detection_method = table.Column<string>(type: "character varying(50)", maxLength: 50, nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_detected_anomalies", x => x.id);
                });

            migrationBuilder.CreateTable(
                name: "endpoint_metrics",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    endpoint_id = table.Column<Guid>(type: "uuid", nullable: false),
                    timestamp = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    latency_ms = table.Column<double>(type: "double precision", nullable: false),
                    packet_loss_percent = table.Column<double>(type: "double precision", nullable: false),
                    availability_percent = table.Column<double>(type: "double precision", nullable: false),
                    error_rate_percent = table.Column<double>(type: "double precision", nullable: false),
                    throughput_mbps = table.Column<double>(type: "double precision", nullable: false),
                    is_reachable = table.Column<bool>(type: "boolean", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_endpoint_metrics", x => x.id);
                });

            migrationBuilder.CreateTable(
                name: "monitored_endpoints",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    name = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: false),
                    url = table.Column<string>(type: "character varying(2048)", maxLength: 2048, nullable: false),
                    status = table.Column<string>(type: "character varying(50)", maxLength: 50, nullable: false),
                    created_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_monitored_endpoints", x => x.id);
                });

            migrationBuilder.CreateIndex(
                name: "IX_detected_anomalies_endpoint_id_detected_at",
                table: "detected_anomalies",
                columns: new[] { "endpoint_id", "detected_at" });

            migrationBuilder.CreateIndex(
                name: "IX_endpoint_metrics_endpoint_id_timestamp",
                table: "endpoint_metrics",
                columns: new[] { "endpoint_id", "timestamp" });

            migrationBuilder.CreateIndex(
                name: "IX_monitored_endpoints_url",
                table: "monitored_endpoints",
                column: "url",
                unique: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "detected_anomalies");

            migrationBuilder.DropTable(
                name: "endpoint_metrics");

            migrationBuilder.DropTable(
                name: "monitored_endpoints");
        }
    }
}
