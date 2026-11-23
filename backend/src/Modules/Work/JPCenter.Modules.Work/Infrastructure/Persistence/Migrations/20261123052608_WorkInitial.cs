using System;
using System.Collections.Generic;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace JPCenter.Modules.Work.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class WorkInitial : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.EnsureSchema(
                name: "work");

            migrationBuilder.CreateTable(
                name: "handovers",
                schema: "work",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    branch_id = table.Column<Guid>(type: "uuid", nullable: false),
                    date = table.Column<DateOnly>(type: "date", nullable: false),
                    from_shift = table.Column<string>(type: "text", nullable: false),
                    from_id = table.Column<Guid>(type: "uuid", nullable: false),
                    to_id = table.Column<Guid>(type: "uuid", nullable: false),
                    cash_expected = table.Column<decimal>(type: "numeric(18,0)", precision: 18, scale: 0, nullable: false),
                    cash_counted = table.Column<decimal>(type: "numeric(18,0)", precision: 18, scale: 0, nullable: false),
                    cash_received = table.Column<decimal>(type: "numeric(18,0)", precision: 18, scale: 0, nullable: true),
                    cash_note = table.Column<string>(type: "character varying(1000)", maxLength: 1000, nullable: true),
                    issues = table.Column<string>(type: "character varying(2000)", maxLength: 2000, nullable: true),
                    status = table.Column<string>(type: "text", nullable: false),
                    handed_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    received_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    receiver_note = table.Column<string>(type: "character varying(1000)", maxLength: 1000, nullable: true),
                    created_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    created_by = table.Column<Guid>(type: "uuid", nullable: true),
                    updated_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    updated_by = table.Column<Guid>(type: "uuid", nullable: true),
                    xmin = table.Column<uint>(type: "xid", rowVersion: true, nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_handovers", x => x.id);
                });

            migrationBuilder.CreateTable(
                name: "outbox_messages",
                schema: "work",
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
                name: "shift_assignments",
                schema: "work",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    employee_id = table.Column<Guid>(type: "uuid", nullable: false),
                    branch_id = table.Column<Guid>(type: "uuid", nullable: false),
                    date = table.Column<DateOnly>(type: "date", nullable: false),
                    shift = table.Column<string>(type: "text", nullable: false),
                    created_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    created_by = table.Column<Guid>(type: "uuid", nullable: true),
                    updated_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    updated_by = table.Column<Guid>(type: "uuid", nullable: true),
                    xmin = table.Column<uint>(type: "xid", rowVersion: true, nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_shift_assignments", x => x.id);
                });

            migrationBuilder.CreateTable(
                name: "shift_registrations",
                schema: "work",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    employee_id = table.Column<Guid>(type: "uuid", nullable: false),
                    date = table.Column<DateOnly>(type: "date", nullable: false),
                    shift = table.Column<string>(type: "text", nullable: false),
                    kind = table.Column<string>(type: "text", nullable: false),
                    updated_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    xmin = table.Column<uint>(type: "xid", rowVersion: true, nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_shift_registrations", x => x.id);
                });

            migrationBuilder.CreateTable(
                name: "tasks",
                schema: "work",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    code = table.Column<string>(type: "character varying(30)", maxLength: 30, nullable: false),
                    title = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: false),
                    description = table.Column<string>(type: "character varying(4000)", maxLength: 4000, nullable: true),
                    branch_id = table.Column<Guid>(type: "uuid", nullable: false),
                    assignee_id = table.Column<Guid>(type: "uuid", nullable: false),
                    creator_id = table.Column<Guid>(type: "uuid", nullable: false),
                    watcher_ids = table.Column<List<Guid>>(type: "uuid[]", nullable: false),
                    priority = table.Column<string>(type: "text", nullable: false),
                    status = table.Column<string>(type: "text", nullable: false),
                    due = table.Column<DateOnly>(type: "date", nullable: true),
                    repeat = table.Column<string>(type: "text", nullable: false),
                    link_type = table.Column<string>(type: "text", nullable: true),
                    link_id = table.Column<Guid>(type: "uuid", nullable: true),
                    link_label = table.Column<string>(type: "character varying(300)", maxLength: 300, nullable: true),
                    completed_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    next_spawned = table.Column<bool>(type: "boolean", nullable: false),
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
                    table.PrimaryKey("pk_tasks", x => x.id);
                });

            migrationBuilder.CreateTable(
                name: "handover_items",
                schema: "work",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    text = table.Column<string>(type: "character varying(1000)", maxLength: 1000, nullable: false),
                    done = table.Column<bool>(type: "boolean", nullable: false),
                    handover_id = table.Column<Guid>(type: "uuid", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_handover_items", x => x.id);
                    table.ForeignKey(
                        name: "fk_handover_items_handovers_handover_id",
                        column: x => x.handover_id,
                        principalSchema: "work",
                        principalTable: "handovers",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "handover_leads",
                schema: "work",
                columns: table => new
                {
                    lead_id = table.Column<Guid>(type: "uuid", nullable: false),
                    handover_id = table.Column<Guid>(type: "uuid", nullable: false),
                    note = table.Column<string>(type: "character varying(1000)", maxLength: 1000, nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_handover_leads", x => new { x.handover_id, x.lead_id });
                    table.ForeignKey(
                        name: "fk_handover_leads_handovers_handover_id",
                        column: x => x.handover_id,
                        principalSchema: "work",
                        principalTable: "handovers",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "task_checklist_items",
                schema: "work",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    text = table.Column<string>(type: "character varying(500)", maxLength: 500, nullable: false),
                    done = table.Column<bool>(type: "boolean", nullable: false),
                    order = table.Column<int>(type: "integer", nullable: false),
                    task_id = table.Column<Guid>(type: "uuid", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_task_checklist_items", x => x.id);
                    table.ForeignKey(
                        name: "fk_task_checklist_items_tasks_task_id",
                        column: x => x.task_id,
                        principalSchema: "work",
                        principalTable: "tasks",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "task_comments",
                schema: "work",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    text = table.Column<string>(type: "character varying(2000)", maxLength: 2000, nullable: false),
                    by = table.Column<Guid>(type: "uuid", nullable: false),
                    by_name = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: false),
                    at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    task_id = table.Column<Guid>(type: "uuid", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_task_comments", x => x.id);
                    table.ForeignKey(
                        name: "fk_task_comments_tasks_task_id",
                        column: x => x.task_id,
                        principalSchema: "work",
                        principalTable: "tasks",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "ix_handover_items_handover_id",
                schema: "work",
                table: "handover_items",
                column: "handover_id");

            migrationBuilder.CreateIndex(
                name: "ix_handovers_branch_id_date_from_shift_from_id",
                schema: "work",
                table: "handovers",
                columns: new[] { "branch_id", "date", "from_shift", "from_id" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "ix_handovers_branch_id_status",
                schema: "work",
                table: "handovers",
                columns: new[] { "branch_id", "status" });

            migrationBuilder.CreateIndex(
                name: "ix_handovers_to_id_status",
                schema: "work",
                table: "handovers",
                columns: new[] { "to_id", "status" });

            migrationBuilder.CreateIndex(
                name: "ix_outbox_messages_next_attempt_at",
                schema: "work",
                table: "outbox_messages",
                column: "next_attempt_at",
                filter: "processed_at IS NULL AND failed_at IS NULL");

            migrationBuilder.CreateIndex(
                name: "ix_shift_assignments_branch_id_date",
                schema: "work",
                table: "shift_assignments",
                columns: new[] { "branch_id", "date" });

            migrationBuilder.CreateIndex(
                name: "ix_shift_assignments_employee_id_date_shift",
                schema: "work",
                table: "shift_assignments",
                columns: new[] { "employee_id", "date", "shift" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "ix_shift_registrations_date",
                schema: "work",
                table: "shift_registrations",
                column: "date");

            migrationBuilder.CreateIndex(
                name: "ix_shift_registrations_employee_id_date_shift",
                schema: "work",
                table: "shift_registrations",
                columns: new[] { "employee_id", "date", "shift" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "ix_task_checklist_items_task_id",
                schema: "work",
                table: "task_checklist_items",
                column: "task_id");

            migrationBuilder.CreateIndex(
                name: "ix_task_comments_task_id",
                schema: "work",
                table: "task_comments",
                column: "task_id");

            migrationBuilder.CreateIndex(
                name: "ix_tasks_assignee_id_status",
                schema: "work",
                table: "tasks",
                columns: new[] { "assignee_id", "status" });

            migrationBuilder.CreateIndex(
                name: "ix_tasks_branch_id_status_due",
                schema: "work",
                table: "tasks",
                columns: new[] { "branch_id", "status", "due" });

            migrationBuilder.CreateIndex(
                name: "ix_tasks_code",
                schema: "work",
                table: "tasks",
                column: "code",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "ix_tasks_creator_id",
                schema: "work",
                table: "tasks",
                column: "creator_id");

            migrationBuilder.CreateIndex(
                name: "ix_tasks_watcher_ids",
                schema: "work",
                table: "tasks",
                column: "watcher_ids")
                .Annotation("Npgsql:IndexMethod", "gin");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "handover_items",
                schema: "work");

            migrationBuilder.DropTable(
                name: "handover_leads",
                schema: "work");

            migrationBuilder.DropTable(
                name: "outbox_messages",
                schema: "work");

            migrationBuilder.DropTable(
                name: "shift_assignments",
                schema: "work");

            migrationBuilder.DropTable(
                name: "shift_registrations",
                schema: "work");

            migrationBuilder.DropTable(
                name: "task_checklist_items",
                schema: "work");

            migrationBuilder.DropTable(
                name: "task_comments",
                schema: "work");

            migrationBuilder.DropTable(
                name: "handovers",
                schema: "work");

            migrationBuilder.DropTable(
                name: "tasks",
                schema: "work");
        }
    }
}
