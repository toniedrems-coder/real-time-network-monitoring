using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace backend.Migrations
{
    /// <inheritdoc />
    public partial class AddWorkflowExecutionDetails : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "email_notification_status",
                table: "workflow_executions",
                type: "character varying(50)",
                maxLength: 50,
                nullable: false,
                defaultValue: "NotConfigured");

            migrationBuilder.AddColumn<string>(
                name: "notification_error",
                table: "workflow_executions",
                type: "text",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "sms_notification_status",
                table: "workflow_executions",
                type: "character varying(50)",
                maxLength: 50,
                nullable: false,
                defaultValue: "NotConfigured");

            migrationBuilder.AddColumn<string>(
                name: "stage_history",
                table: "workflow_executions",
                type: "jsonb",
                nullable: false,
                defaultValueSql: "'[]'::jsonb");

            migrationBuilder.AddColumn<string>(
                name: "webhook_notification_status",
                table: "workflow_executions",
                type: "character varying(50)",
                maxLength: 50,
                nullable: false,
                defaultValue: "NotConfigured");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "email_notification_status",
                table: "workflow_executions");

            migrationBuilder.DropColumn(
                name: "notification_error",
                table: "workflow_executions");

            migrationBuilder.DropColumn(
                name: "sms_notification_status",
                table: "workflow_executions");

            migrationBuilder.DropColumn(
                name: "stage_history",
                table: "workflow_executions");

            migrationBuilder.DropColumn(
                name: "webhook_notification_status",
                table: "workflow_executions");
        }
    }
}
