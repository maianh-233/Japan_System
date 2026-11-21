using System;
using System.Collections.Generic;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace JPCenter.Modules.Crm.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class CrmAdmissions : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "appointments",
                schema: "crm",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    code = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: false),
                    lead_id = table.Column<Guid>(type: "uuid", nullable: false),
                    kind = table.Column<string>(type: "text", nullable: false),
                    branch_id = table.Column<Guid>(type: "uuid", nullable: false),
                    room_id = table.Column<Guid>(type: "uuid", nullable: true),
                    staff_id = table.Column<Guid>(type: "uuid", nullable: false),
                    start = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    end = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    status = table.Column<string>(type: "text", nullable: false),
                    note = table.Column<string>(type: "character varying(1000)", maxLength: 1000, nullable: true),
                    outcome = table.Column<string>(type: "character varying(2000)", maxLength: 2000, nullable: true),
                    reminded_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    cancel_reason = table.Column<string>(type: "character varying(500)", maxLength: 500, nullable: true),
                    created_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    created_by = table.Column<Guid>(type: "uuid", nullable: true),
                    updated_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    updated_by = table.Column<Guid>(type: "uuid", nullable: true),
                    xmin = table.Column<uint>(type: "xid", rowVersion: true, nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_appointments", x => x.id);
                    table.ForeignKey(
                        name: "fk_appointments_leads_lead_id",
                        column: x => x.lead_id,
                        principalSchema: "crm",
                        principalTable: "leads",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "campaigns",
                schema: "crm",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    code = table.Column<string>(type: "character varying(30)", maxLength: 30, nullable: false),
                    name = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: false),
                    channel = table.Column<string>(type: "text", nullable: false),
                    branch_ids = table.Column<Guid[]>(type: "uuid[]", nullable: false),
                    start_date = table.Column<DateOnly>(type: "date", nullable: false),
                    end_date = table.Column<DateOnly>(type: "date", nullable: false),
                    budget = table.Column<decimal>(type: "numeric(18,0)", precision: 18, scale: 0, nullable: false),
                    spent = table.Column<decimal>(type: "numeric(18,0)", precision: 18, scale: 0, nullable: false),
                    target_leads = table.Column<int>(type: "integer", nullable: true),
                    status = table.Column<string>(type: "text", nullable: false),
                    owner_id = table.Column<Guid>(type: "uuid", nullable: false),
                    utm = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: true),
                    course_ids = table.Column<List<Guid>>(type: "uuid[]", nullable: false),
                    note = table.Column<string>(type: "character varying(2000)", maxLength: 2000, nullable: true),
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
                    table.PrimaryKey("pk_campaigns", x => x.id);
                });

            migrationBuilder.CreateTable(
                name: "lead_rules",
                schema: "crm",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    name = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: false),
                    priority = table.Column<int>(type: "integer", nullable: false),
                    active = table.Column<bool>(type: "boolean", nullable: false),
                    sources = table.Column<string[]>(type: "text[]", nullable: false),
                    branch_ids = table.Column<Guid[]>(type: "uuid[]", nullable: false),
                    interests = table.Column<List<string>>(type: "text[]", nullable: false),
                    method = table.Column<string>(type: "text", nullable: false),
                    sale_ids = table.Column<List<Guid>>(type: "uuid[]", nullable: false),
                    only_on_shift = table.Column<bool>(type: "boolean", nullable: false),
                    sla_minutes = table.Column<int>(type: "integer", nullable: false),
                    escalate_to_ids = table.Column<List<Guid>>(type: "uuid[]", nullable: false),
                    reassign_on_breach = table.Column<bool>(type: "boolean", nullable: false),
                    cursor = table.Column<int>(type: "integer", nullable: false),
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
                    table.PrimaryKey("pk_lead_rules", x => x.id);
                });

            migrationBuilder.CreateTable(
                name: "message_templates",
                schema: "crm",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    scenario = table.Column<string>(type: "text", nullable: false),
                    name = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: false),
                    channel = table.Column<string>(type: "text", nullable: false),
                    body = table.Column<string>(type: "character varying(2000)", maxLength: 2000, nullable: false),
                    active = table.Column<bool>(type: "boolean", nullable: false),
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
                    table.PrimaryKey("pk_message_templates", x => x.id);
                });

            migrationBuilder.CreateTable(
                name: "placement_tests",
                schema: "crm",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    code = table.Column<string>(type: "character varying(30)", maxLength: 30, nullable: false),
                    name = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: false),
                    description = table.Column<string>(type: "character varying(2000)", maxLength: 2000, nullable: true),
                    sections = table.Column<string>(type: "jsonb", nullable: false),
                    question_ids = table.Column<List<Guid>>(type: "uuid[]", nullable: false),
                    duration_minutes = table.Column<int>(type: "integer", nullable: false),
                    bands = table.Column<string>(type: "jsonb", nullable: false),
                    status = table.Column<string>(type: "text", nullable: false),
                    author_id = table.Column<Guid>(type: "uuid", nullable: false),
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
                    table.PrimaryKey("pk_placement_tests", x => x.id);
                });

            migrationBuilder.CreateTable(
                name: "lead_assignment_logs",
                schema: "crm",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    lead_id = table.Column<Guid>(type: "uuid", nullable: false),
                    sale_id = table.Column<Guid>(type: "uuid", nullable: true),
                    rule_id = table.Column<Guid>(type: "uuid", nullable: true),
                    at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    reason = table.Column<string>(type: "character varying(500)", maxLength: 500, nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_lead_assignment_logs", x => x.id);
                    table.ForeignKey(
                        name: "fk_lead_assignment_logs_lead_rules_rule_id",
                        column: x => x.rule_id,
                        principalSchema: "crm",
                        principalTable: "lead_rules",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "fk_lead_assignment_logs_leads_lead_id",
                        column: x => x.lead_id,
                        principalSchema: "crm",
                        principalTable: "leads",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "message_logs",
                schema: "crm",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    channel = table.Column<string>(type: "text", nullable: false),
                    scenario = table.Column<string>(type: "text", nullable: false),
                    template_id = table.Column<Guid>(type: "uuid", nullable: true),
                    to_name = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: false),
                    to_phone = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: false),
                    lead_id = table.Column<Guid>(type: "uuid", nullable: true),
                    student_id = table.Column<Guid>(type: "uuid", nullable: true),
                    body = table.Column<string>(type: "character varying(4000)", maxLength: 4000, nullable: false),
                    sent_by = table.Column<Guid>(type: "uuid", nullable: true),
                    sent_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    auto = table.Column<bool>(type: "boolean", nullable: false),
                    status = table.Column<string>(type: "text", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_message_logs", x => x.id);
                    table.ForeignKey(
                        name: "fk_message_logs_leads_lead_id",
                        column: x => x.lead_id,
                        principalSchema: "crm",
                        principalTable: "leads",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "fk_message_logs_message_templates_template_id",
                        column: x => x.template_id,
                        principalSchema: "crm",
                        principalTable: "message_templates",
                        principalColumn: "id",
                        onDelete: ReferentialAction.SetNull);
                });

            migrationBuilder.CreateTable(
                name: "placement_attempts",
                schema: "crm",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    test_id = table.Column<Guid>(type: "uuid", nullable: false),
                    lead_id = table.Column<Guid>(type: "uuid", nullable: false),
                    token = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: false),
                    channel = table.Column<string>(type: "text", nullable: false),
                    sent_by = table.Column<Guid>(type: "uuid", nullable: false),
                    sent_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    expires_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    status = table.Column<string>(type: "text", nullable: false),
                    started_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    submitted_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    answers = table.Column<string>(type: "jsonb", nullable: false),
                    correct = table.Column<int>(type: "integer", nullable: true),
                    total = table.Column<int>(type: "integer", nullable: true),
                    by_skill = table.Column<string>(type: "jsonb", nullable: false),
                    suggested_level = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: true),
                    suggested_course = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: true),
                    logged = table.Column<bool>(type: "boolean", nullable: false),
                    xmin = table.Column<uint>(type: "xid", rowVersion: true, nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_placement_attempts", x => x.id);
                    table.ForeignKey(
                        name: "fk_placement_attempts_leads_lead_id",
                        column: x => x.lead_id,
                        principalSchema: "crm",
                        principalTable: "leads",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "fk_placement_attempts_placement_tests_test_id",
                        column: x => x.test_id,
                        principalSchema: "crm",
                        principalTable: "placement_tests",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateIndex(
                name: "ix_leads_campaign_id",
                schema: "crm",
                table: "leads",
                column: "campaign_id");

            migrationBuilder.CreateIndex(
                name: "ix_appointments_branch_id_start",
                schema: "crm",
                table: "appointments",
                columns: new[] { "branch_id", "start" });

            migrationBuilder.CreateIndex(
                name: "ix_appointments_code",
                schema: "crm",
                table: "appointments",
                column: "code",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "ix_appointments_lead_id",
                schema: "crm",
                table: "appointments",
                column: "lead_id");

            migrationBuilder.CreateIndex(
                name: "ix_appointments_staff_id_start",
                schema: "crm",
                table: "appointments",
                columns: new[] { "staff_id", "start" });

            migrationBuilder.CreateIndex(
                name: "ix_appointments_status_start",
                schema: "crm",
                table: "appointments",
                columns: new[] { "status", "start" });

            migrationBuilder.CreateIndex(
                name: "ix_campaigns_code",
                schema: "crm",
                table: "campaigns",
                column: "code",
                unique: true,
                filter: "is_deleted = false");

            migrationBuilder.CreateIndex(
                name: "ix_campaigns_utm",
                schema: "crm",
                table: "campaigns",
                column: "utm",
                unique: true,
                filter: "is_deleted = false AND utm IS NOT NULL");

            migrationBuilder.CreateIndex(
                name: "ix_lead_assignment_logs_at",
                schema: "crm",
                table: "lead_assignment_logs",
                column: "at");

            migrationBuilder.CreateIndex(
                name: "ix_lead_assignment_logs_lead_id",
                schema: "crm",
                table: "lead_assignment_logs",
                column: "lead_id");

            migrationBuilder.CreateIndex(
                name: "ix_lead_assignment_logs_rule_id",
                schema: "crm",
                table: "lead_assignment_logs",
                column: "rule_id");

            migrationBuilder.CreateIndex(
                name: "ix_lead_rules_priority",
                schema: "crm",
                table: "lead_rules",
                column: "priority");

            migrationBuilder.CreateIndex(
                name: "ix_message_logs_lead_id_sent_at",
                schema: "crm",
                table: "message_logs",
                columns: new[] { "lead_id", "sent_at" });

            migrationBuilder.CreateIndex(
                name: "ix_message_logs_status",
                schema: "crm",
                table: "message_logs",
                column: "status");

            migrationBuilder.CreateIndex(
                name: "ix_message_logs_student_id_sent_at",
                schema: "crm",
                table: "message_logs",
                columns: new[] { "student_id", "sent_at" });

            migrationBuilder.CreateIndex(
                name: "ix_message_logs_template_id",
                schema: "crm",
                table: "message_logs",
                column: "template_id");

            migrationBuilder.CreateIndex(
                name: "ix_message_templates_scenario",
                schema: "crm",
                table: "message_templates",
                column: "scenario");

            migrationBuilder.CreateIndex(
                name: "ix_placement_attempts_lead_id",
                schema: "crm",
                table: "placement_attempts",
                column: "lead_id");

            migrationBuilder.CreateIndex(
                name: "ix_placement_attempts_status_expires_at",
                schema: "crm",
                table: "placement_attempts",
                columns: new[] { "status", "expires_at" });

            migrationBuilder.CreateIndex(
                name: "ix_placement_attempts_test_id",
                schema: "crm",
                table: "placement_attempts",
                column: "test_id");

            migrationBuilder.CreateIndex(
                name: "ix_placement_attempts_token",
                schema: "crm",
                table: "placement_attempts",
                column: "token",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "ix_placement_tests_code",
                schema: "crm",
                table: "placement_tests",
                column: "code",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "ix_placement_tests_status",
                schema: "crm",
                table: "placement_tests",
                column: "status");

            migrationBuilder.AddForeignKey(
                name: "fk_leads_campaigns_campaign_id",
                schema: "crm",
                table: "leads",
                column: "campaign_id",
                principalSchema: "crm",
                principalTable: "campaigns",
                principalColumn: "id",
                onDelete: ReferentialAction.Restrict);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "fk_leads_campaigns_campaign_id",
                schema: "crm",
                table: "leads");

            migrationBuilder.DropTable(
                name: "appointments",
                schema: "crm");

            migrationBuilder.DropTable(
                name: "campaigns",
                schema: "crm");

            migrationBuilder.DropTable(
                name: "lead_assignment_logs",
                schema: "crm");

            migrationBuilder.DropTable(
                name: "message_logs",
                schema: "crm");

            migrationBuilder.DropTable(
                name: "placement_attempts",
                schema: "crm");

            migrationBuilder.DropTable(
                name: "lead_rules",
                schema: "crm");

            migrationBuilder.DropTable(
                name: "message_templates",
                schema: "crm");

            migrationBuilder.DropTable(
                name: "placement_tests",
                schema: "crm");

            migrationBuilder.DropIndex(
                name: "ix_leads_campaign_id",
                schema: "crm",
                table: "leads");
        }
    }
}
