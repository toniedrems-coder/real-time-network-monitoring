using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace backend.Migrations
{
    /// <inheritdoc />
    public partial class AddKpiSnapshots : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "kpi_snapshots",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    timestamp = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    total_endpoints = table.Column<int>(type: "integer", nullable: false),
                    healthy_endpoints = table.Column<int>(type: "integer", nullable: false),
                    healthy_percent = table.Column<double>(type: "double precision", nullable: false),
                    average_latency_ms = table.Column<double>(type: "double precision", nullable: false),
                    p95_latency_ms = table.Column<double>(type: "double precision", nullable: false),
                    active_anomalies = table.Column<int>(type: "integer", nullable: false),
                    anomaly_rate_per_hour = table.Column<double>(type: "double precision", nullable: false),
                    endpoints_at_risk = table.Column<int>(type: "integer", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_kpi_snapshots", x => x.id);
                });

            migrationBuilder.CreateIndex(
                name: "IX_kpi_snapshots_timestamp",
                table: "kpi_snapshots",
                column: "timestamp");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "kpi_snapshots");
        }
    }
}
