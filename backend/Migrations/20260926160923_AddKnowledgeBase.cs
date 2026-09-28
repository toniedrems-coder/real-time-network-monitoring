using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace backend.Migrations
{
    /// <inheritdoc />
    public partial class AddKnowledgeBase : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "knowledge_articles",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    article_number = table.Column<string>(type: "character varying(50)", maxLength: 50, nullable: false),
                    title = table.Column<string>(type: "character varying(250)", maxLength: 250, nullable: false),
                    description = table.Column<string>(type: "text", nullable: false),
                    type = table.Column<string>(type: "character varying(50)", maxLength: 50, nullable: false),
                    target = table.Column<string>(type: "character varying(500)", maxLength: 500, nullable: true),
                    target_type = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: true),
                    failure_type = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: true),
                    root_cause = table.Column<string>(type: "text", nullable: true),
                    resolution = table.Column<string>(type: "text", nullable: true),
                    recommended_actions = table.Column<string>(type: "text", nullable: true),
                    runbook_reference = table.Column<string>(type: "character varying(500)", maxLength: 500, nullable: true),
                    tags = table.Column<string>(type: "character varying(1000)", maxLength: 1000, nullable: true),
                    source_incident_id = table.Column<Guid>(type: "uuid", nullable: true),
                    confidence = table.Column<double>(type: "double precision", nullable: false),
                    times_matched = table.Column<int>(type: "integer", nullable: false),
                    times_successful = table.Column<int>(type: "integer", nullable: false),
                    is_active = table.Column<bool>(type: "boolean", nullable: false),
                    created_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    updated_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    last_used_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_knowledge_articles", x => x.id);
                });

            migrationBuilder.CreateIndex(
                name: "IX_knowledge_articles_article_number",
                table: "knowledge_articles",
                column: "article_number",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_knowledge_articles_failure_type",
                table: "knowledge_articles",
                column: "failure_type");

            migrationBuilder.CreateIndex(
                name: "IX_knowledge_articles_is_active",
                table: "knowledge_articles",
                column: "is_active");

            migrationBuilder.CreateIndex(
                name: "IX_knowledge_articles_source_incident_id",
                table: "knowledge_articles",
                column: "source_incident_id");

            migrationBuilder.CreateIndex(
                name: "IX_knowledge_articles_target",
                table: "knowledge_articles",
                column: "target");

            migrationBuilder.CreateIndex(
                name: "IX_knowledge_articles_type",
                table: "knowledge_articles",
                column: "type");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "knowledge_articles");
        }
    }
}
