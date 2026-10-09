using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace backend.Migrations
{
    /// <inheritdoc />
    public partial class AddIncidentKnowledgeMatches : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "incident_knowledge_matches",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    incident_id = table.Column<Guid>(type: "uuid", nullable: false),
                    knowledge_article_id = table.Column<Guid>(type: "uuid", nullable: false),
                    match_score = table.Column<double>(type: "double precision", nullable: false),
                    match_level = table.Column<string>(type: "character varying(50)", maxLength: 50, nullable: false),
                    match_reasons = table.Column<string>(type: "text", nullable: true),
                    was_selected = table.Column<bool>(type: "boolean", nullable: false),
                    was_successful = table.Column<bool>(type: "boolean", nullable: true),
                    matched_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    verified_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    created_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    updated_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_incident_knowledge_matches", x => x.id);
                    table.ForeignKey(
                        name: "FK_incident_knowledge_matches_incidents_incident_id",
                        column: x => x.incident_id,
                        principalTable: "incidents",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_incident_knowledge_matches_knowledge_articles_knowledge_art~",
                        column: x => x.knowledge_article_id,
                        principalTable: "knowledge_articles",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateIndex(
                name: "IX_incident_knowledge_matches_incident_id",
                table: "incident_knowledge_matches",
                column: "incident_id");

            migrationBuilder.CreateIndex(
                name: "IX_incident_knowledge_matches_incident_id_knowledge_article_id",
                table: "incident_knowledge_matches",
                columns: new[] { "incident_id", "knowledge_article_id" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_incident_knowledge_matches_incident_id_was_selected",
                table: "incident_knowledge_matches",
                columns: new[] { "incident_id", "was_selected" });

            migrationBuilder.CreateIndex(
                name: "IX_incident_knowledge_matches_knowledge_article_id",
                table: "incident_knowledge_matches",
                column: "knowledge_article_id");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "incident_knowledge_matches");
        }
    }
}
