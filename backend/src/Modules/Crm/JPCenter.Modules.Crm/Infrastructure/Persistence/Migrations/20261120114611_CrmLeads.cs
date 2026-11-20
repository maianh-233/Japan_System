using System;
using System.Collections.Generic;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace JPCenter.Modules.Crm.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class CrmLeads : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.EnsureSchema(
                name: "crm");

            migrationBuilder.CreateTable(
                name: "leads",
                schema: "crm",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    code = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: false),
                    full_name = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: false),
                    phone = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: false),
                    email = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: true),
                    source = table.Column<string>(type: "text", nullable: false),
                    status = table.Column<string>(type: "text", nullable: false),
                    branch_id = table.Column<Guid>(type: "uuid", nullable: false),
                    assignee_id = table.Column<Guid>(type: "uuid", nullable: true),
                    interest = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: true),
                    note = table.Column<string>(type: "character varying(2000)", maxLength: 2000, nullable: true),
                    lost_reason = table.Column<string>(type: "character varying(500)", maxLength: 500, nullable: true),
                    next_follow_up_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    last_contact_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    hidden = table.Column<bool>(type: "boolean", nullable: false),
                    hidden_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    notify_channels = table.Column<string[]>(type: "text[]", nullable: false),
                    campaign_id = table.Column<Guid>(type: "uuid", nullable: true),
                    sla_escalated_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    sla_reassigned_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    created_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    created_by = table.Column<Guid>(type: "uuid", nullable: true),
                    deleted_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    deleted_by = table.Column<Guid>(type: "uuid", nullable: true),
                    is_deleted = table.Column<bool>(type: "boolean", nullable: false, defaultValue: false),
                    updated_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    updated_by = table.Column<Guid>(type: "uuid", nullable: true),
                    xmin = table.Column<uint>(type: "xid", rowVersion: true, nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_leads", x => x.id);
                });

            migrationBuilder.CreateTable(
                name: "outbox_messages",
                schema: "crm",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    type = table.Column<string>(type: "character varying(300)", maxLength: 300, nullable: false),
                    payload = table.Column<string>(type: "jsonb", nullable: false),
                    occurred_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    processed_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    attempts = table.Column<int>(type: "integer", nullable: false),
                    next_attempt_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    last_error = table.Column<string>(type: "character varying(2000)", maxLength: 2000, nullable: true),
                    handled_by = table.Column<List<string>>(type: "text[]", nullable: false),
                    failed_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_outbox_messages", x => x.id);
                });

            migrationBuilder.CreateTable(
                name: "consultations",
                schema: "crm",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    channel = table.Column<string>(type: "text", nullable: false),
                    result = table.Column<string>(type: "text", nullable: false),
                    content = table.Column<string>(type: "character varying(4000)", maxLength: 4000, nullable: false),
                    staff_id = table.Column<Guid>(type: "uuid", nullable: true),
                    staff_name = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: false),
                    created_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    next_follow_up_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    is_system = table.Column<bool>(type: "boolean", nullable: false),
                    lead_id = table.Column<Guid>(type: "uuid", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_consultations", x => x.id);
                    table.ForeignKey(
                        name: "fk_consultations_leads_lead_id",
                        column: x => x.lead_id,
                        principalSchema: "crm",
                        principalTable: "leads",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "lead_student_links",
                schema: "crm",
                columns: table => new
                {
                    student_id = table.Column<Guid>(type: "uuid", nullable: false),
                    lead_id = table.Column<Guid>(type: "uuid", nullable: false),
                    relationship = table.Column<string>(type: "text", nullable: false),
                    is_primary = table.Column<bool>(type: "boolean", nullable: false),
                    billing = table.Column<bool>(type: "boolean", nullable: false),
                    notify = table.Column<bool>(type: "boolean", nullable: false),
                    created_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_lead_student_links", x => new { x.lead_id, x.student_id });
                    table.ForeignKey(
                        name: "fk_lead_student_links_leads_lead_id",
                        column: x => x.lead_id,
                        principalSchema: "crm",
                        principalTable: "leads",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "ix_consultations_lead_id",
                schema: "crm",
                table: "consultations",
                column: "lead_id");

            migrationBuilder.CreateIndex(
                name: "ix_lead_student_links_student_id",
                schema: "crm",
                table: "lead_student_links",
                column: "student_id");

            migrationBuilder.CreateIndex(
                name: "ix_leads_assignee_id",
                schema: "crm",
                table: "leads",
                column: "assignee_id");

            migrationBuilder.CreateIndex(
                name: "ix_leads_branch_id_status",
                schema: "crm",
                table: "leads",
                columns: new[] { "branch_id", "status" });

            migrationBuilder.CreateIndex(
                name: "ix_leads_code",
                schema: "crm",
                table: "leads",
                column: "code",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "ix_leads_phone",
                schema: "crm",
                table: "leads",
                column: "phone");

            migrationBuilder.CreateIndex(
                name: "ix_outbox_messages_next_attempt_at",
                schema: "crm",
                table: "outbox_messages",
                column: "next_attempt_at",
                filter: "processed_at IS NULL AND failed_at IS NULL");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "consultations",
                schema: "crm");

            migrationBuilder.DropTable(
                name: "lead_student_links",
                schema: "crm");

            migrationBuilder.DropTable(
                name: "outbox_messages",
                schema: "crm");

            migrationBuilder.DropTable(
                name: "leads",
                schema: "crm");
        }
    }
}
