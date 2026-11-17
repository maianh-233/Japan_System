using System;
using System.Collections.Generic;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace JPCenter.Modules.Academic.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class AcademicClassesSessions : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "rooms",
                schema: "academic",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    branch_id = table.Column<Guid>(type: "uuid", nullable: false),
                    name = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: false),
                    floor = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: true),
                    type = table.Column<string>(type: "text", nullable: false),
                    capacity = table.Column<int>(type: "integer", nullable: false),
                    area = table.Column<decimal>(type: "numeric(18,2)", precision: 18, scale: 2, nullable: true),
                    equipment = table.Column<string[]>(type: "text[]", nullable: false),
                    status = table.Column<string>(type: "text", nullable: false),
                    maintenance_reason = table.Column<string>(type: "character varying(500)", maxLength: 500, nullable: true),
                    maintenance_until = table.Column<DateOnly>(type: "date", nullable: true),
                    note = table.Column<string>(type: "character varying(1000)", maxLength: 1000, nullable: true),
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
                    table.PrimaryKey("pk_rooms", x => x.id);
                });

            migrationBuilder.CreateTable(
                name: "classes",
                schema: "academic",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    course_id = table.Column<Guid>(type: "uuid", nullable: false),
                    curriculum_id = table.Column<Guid>(type: "uuid", nullable: true),
                    code = table.Column<string>(type: "character varying(40)", maxLength: 40, nullable: false),
                    name = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: false),
                    branch_id = table.Column<Guid>(type: "uuid", nullable: false),
                    teacher_ids = table.Column<List<Guid>>(type: "uuid[]", nullable: false),
                    assistant_ids = table.Column<List<Guid>>(type: "uuid[]", nullable: false),
                    room_id = table.Column<Guid>(type: "uuid", nullable: true),
                    online = table.Column<bool>(type: "boolean", nullable: false),
                    meeting_url = table.Column<string>(type: "character varying(500)", maxLength: 500, nullable: true),
                    planned_start_date = table.Column<DateOnly>(type: "date", nullable: false),
                    planned_end_date = table.Column<DateOnly>(type: "date", nullable: false),
                    actual_start_date = table.Column<DateOnly>(type: "date", nullable: true),
                    actual_end_date = table.Column<DateOnly>(type: "date", nullable: true),
                    weekdays = table.Column<List<int>>(type: "integer[]", nullable: false),
                    start_time = table.Column<TimeOnly>(type: "time without time zone", nullable: false),
                    end_time = table.Column<TimeOnly>(type: "time without time zone", nullable: false),
                    total_hours = table.Column<decimal>(type: "numeric(7,2)", precision: 7, scale: 2, nullable: false),
                    min_students = table.Column<int>(type: "integer", nullable: false),
                    max_students = table.Column<int>(type: "integer", nullable: false),
                    fee = table.Column<decimal>(type: "numeric(18,0)", precision: 18, scale: 0, nullable: false),
                    status = table.Column<string>(type: "text", nullable: false),
                    previous_class_id = table.Column<Guid>(type: "uuid", nullable: true),
                    pause_reason = table.Column<string>(type: "character varying(500)", maxLength: 500, nullable: true),
                    cancel_reason = table.Column<string>(type: "character varying(500)", maxLength: 500, nullable: true),
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
                    table.PrimaryKey("pk_classes", x => x.id);
                    table.ForeignKey(
                        name: "fk_classes_classes_previous_class_id",
                        column: x => x.previous_class_id,
                        principalSchema: "academic",
                        principalTable: "classes",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "fk_classes_courses_course_id",
                        column: x => x.course_id,
                        principalSchema: "academic",
                        principalTable: "courses",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "fk_classes_curricula_curriculum_id",
                        column: x => x.curriculum_id,
                        principalSchema: "academic",
                        principalTable: "curricula",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "fk_classes_rooms_room_id",
                        column: x => x.room_id,
                        principalSchema: "academic",
                        principalTable: "rooms",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "room_issues",
                schema: "academic",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    room_id = table.Column<Guid>(type: "uuid", nullable: false),
                    equipment = table.Column<string>(type: "text", nullable: true),
                    title = table.Column<string>(type: "character varying(300)", maxLength: 300, nullable: false),
                    description = table.Column<string>(type: "character varying(2000)", maxLength: 2000, nullable: true),
                    severity = table.Column<string>(type: "text", nullable: false),
                    status = table.Column<string>(type: "text", nullable: false),
                    reported_by = table.Column<Guid>(type: "uuid", nullable: false),
                    reported_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    assignee = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: true),
                    due_date = table.Column<DateOnly>(type: "date", nullable: true),
                    started_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    resolved_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    resolution = table.Column<string>(type: "character varying(2000)", maxLength: 2000, nullable: true),
                    cost = table.Column<decimal>(type: "numeric(18,0)", precision: 18, scale: 0, nullable: true),
                    created_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    created_by = table.Column<Guid>(type: "uuid", nullable: true),
                    updated_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    updated_by = table.Column<Guid>(type: "uuid", nullable: true),
                    xmin = table.Column<uint>(type: "xid", rowVersion: true, nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_room_issues", x => x.id);
                    table.ForeignKey(
                        name: "fk_room_issues_rooms_room_id",
                        column: x => x.room_id,
                        principalSchema: "academic",
                        principalTable: "rooms",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "class_sessions",
                schema: "academic",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    class_id = table.Column<Guid>(type: "uuid", nullable: false),
                    branch_id = table.Column<Guid>(type: "uuid", nullable: false),
                    no = table.Column<int>(type: "integer", nullable: false),
                    date = table.Column<DateOnly>(type: "date", nullable: false),
                    start_time = table.Column<TimeOnly>(type: "time without time zone", nullable: false),
                    end_time = table.Column<TimeOnly>(type: "time without time zone", nullable: false),
                    kind = table.Column<string>(type: "text", nullable: false),
                    lesson_id = table.Column<Guid>(type: "uuid", nullable: true),
                    exam_id = table.Column<Guid>(type: "uuid", nullable: true),
                    status = table.Column<string>(type: "text", nullable: false),
                    origin = table.Column<string>(type: "text", nullable: false),
                    makeup_for_id = table.Column<Guid>(type: "uuid", nullable: true),
                    teacher_ids = table.Column<List<Guid>>(type: "uuid[]", nullable: false),
                    assistant_ids = table.Column<List<Guid>>(type: "uuid[]", nullable: true),
                    room_id = table.Column<Guid>(type: "uuid", nullable: true),
                    attendance_qr_token = table.Column<string>(type: "character varying(64)", maxLength: 64, nullable: false),
                    note = table.Column<string>(type: "character varying(1000)", maxLength: 1000, nullable: true),
                    rescheduled_from_date = table.Column<DateOnly>(type: "date", nullable: true),
                    rescheduled_from_start = table.Column<TimeOnly>(type: "time without time zone", nullable: true),
                    rescheduled_from_end = table.Column<TimeOnly>(type: "time without time zone", nullable: true),
                    reschedule_reason = table.Column<string>(type: "character varying(500)", maxLength: 500, nullable: true),
                    cancelled_by = table.Column<Guid>(type: "uuid", nullable: true),
                    cancelled_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    attendance_by = table.Column<Guid>(type: "uuid", nullable: true),
                    attendance_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    confirmed_by = table.Column<Guid>(type: "uuid", nullable: true),
                    confirmed_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    confirm_note = table.Column<string>(type: "character varying(1000)", maxLength: 1000, nullable: true),
                    incident_at = table.Column<TimeOnly>(type: "time without time zone", nullable: true),
                    incident_reason = table.Column<string>(type: "text", nullable: true),
                    incident_detail = table.Column<string>(type: "character varying(1000)", maxLength: 1000, nullable: true),
                    incident_action = table.Column<string>(type: "text", nullable: true),
                    incident_takeover_id = table.Column<Guid>(type: "uuid", nullable: true),
                    incident_notified = table.Column<bool>(type: "boolean", nullable: true),
                    incident_reported_by = table.Column<Guid>(type: "uuid", nullable: true),
                    incident_reported_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    incident_outcome = table.Column<string>(type: "text", nullable: true),
                    incident_outcome_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    incident_outcome_teacher_id = table.Column<Guid>(type: "uuid", nullable: true),
                    incident_outcome_by = table.Column<Guid>(type: "uuid", nullable: true),
                    report_content = table.Column<string>(type: "character varying(4000)", maxLength: 4000, nullable: true),
                    report_homework = table.Column<string>(type: "character varying(2000)", maxLength: 2000, nullable: true),
                    report_issues = table.Column<string>(type: "character varying(2000)", maxLength: 2000, nullable: true),
                    report_coverage = table.Column<string>(type: "text", nullable: true),
                    report_covered_chapter_ids = table.Column<Guid[]>(type: "uuid[]", nullable: true),
                    report_ended_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    report_author_id = table.Column<Guid>(type: "uuid", nullable: true),
                    report_updated_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    created_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    created_by = table.Column<Guid>(type: "uuid", nullable: true),
                    updated_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    updated_by = table.Column<Guid>(type: "uuid", nullable: true),
                    xmin = table.Column<uint>(type: "xid", rowVersion: true, nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_class_sessions", x => x.id);
                    table.ForeignKey(
                        name: "fk_class_sessions_class_sessions_makeup_for_id",
                        column: x => x.makeup_for_id,
                        principalSchema: "academic",
                        principalTable: "class_sessions",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "fk_class_sessions_classes_class_id",
                        column: x => x.class_id,
                        principalSchema: "academic",
                        principalTable: "classes",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "fk_class_sessions_rooms_room_id",
                        column: x => x.room_id,
                        principalSchema: "academic",
                        principalTable: "rooms",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "attendance_records",
                schema: "academic",
                columns: table => new
                {
                    student_id = table.Column<Guid>(type: "uuid", nullable: false),
                    class_session_id = table.Column<Guid>(type: "uuid", nullable: false),
                    status = table.Column<string>(type: "text", nullable: false),
                    note = table.Column<string>(type: "character varying(500)", maxLength: 500, nullable: true),
                    check_in_at = table.Column<TimeOnly>(type: "time without time zone", nullable: true),
                    method = table.Column<string>(type: "text", nullable: true),
                    left_at = table.Column<TimeOnly>(type: "time without time zone", nullable: true),
                    leave_reason = table.Column<string>(type: "character varying(500)", maxLength: 500, nullable: true),
                    leave_request_id = table.Column<Guid>(type: "uuid", nullable: true),
                    made_up_session_id = table.Column<Guid>(type: "uuid", nullable: true),
                    made_up_status = table.Column<string>(type: "text", nullable: true),
                    makeup_for_session_id = table.Column<Guid>(type: "uuid", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_attendance_records", x => new { x.class_session_id, x.student_id });
                    table.ForeignKey(
                        name: "fk_attendance_records_class_sessions_class_session_id",
                        column: x => x.class_session_id,
                        principalSchema: "academic",
                        principalTable: "class_sessions",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "session_support_entries",
                schema: "academic",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    kind = table.Column<string>(type: "text", nullable: false),
                    note = table.Column<string>(type: "character varying(1000)", maxLength: 1000, nullable: false),
                    by = table.Column<Guid>(type: "uuid", nullable: false),
                    at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    class_session_id = table.Column<Guid>(type: "uuid", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_session_support_entries", x => x.id);
                    table.ForeignKey(
                        name: "fk_session_support_entries_class_sessions_class_session_id",
                        column: x => x.class_session_id,
                        principalSchema: "academic",
                        principalTable: "class_sessions",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "teacher_attendances",
                schema: "academic",
                columns: table => new
                {
                    teacher_id = table.Column<Guid>(type: "uuid", nullable: false),
                    class_session_id = table.Column<Guid>(type: "uuid", nullable: false),
                    role = table.Column<string>(type: "text", nullable: false),
                    source = table.Column<string>(type: "text", nullable: false),
                    checked_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    status = table.Column<string>(type: "text", nullable: false),
                    check_in = table.Column<TimeOnly>(type: "time without time zone", nullable: true),
                    check_out = table.Column<TimeOnly>(type: "time without time zone", nullable: true),
                    substitute_id = table.Column<Guid>(type: "uuid", nullable: true),
                    note = table.Column<string>(type: "character varying(500)", maxLength: 500, nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_teacher_attendances", x => new { x.class_session_id, x.teacher_id });
                    table.ForeignKey(
                        name: "fk_teacher_attendances_class_sessions_class_session_id",
                        column: x => x.class_session_id,
                        principalSchema: "academic",
                        principalTable: "class_sessions",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "ix_attendance_records_student_id",
                schema: "academic",
                table: "attendance_records",
                column: "student_id");

            migrationBuilder.CreateIndex(
                name: "ix_class_sessions_attendance_qr_token",
                schema: "academic",
                table: "class_sessions",
                column: "attendance_qr_token",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "ix_class_sessions_branch_id_date",
                schema: "academic",
                table: "class_sessions",
                columns: new[] { "branch_id", "date" });

            migrationBuilder.CreateIndex(
                name: "ix_class_sessions_class_id_date",
                schema: "academic",
                table: "class_sessions",
                columns: new[] { "class_id", "date" });

            migrationBuilder.CreateIndex(
                name: "ix_class_sessions_makeup_for_id",
                schema: "academic",
                table: "class_sessions",
                column: "makeup_for_id");

            migrationBuilder.CreateIndex(
                name: "ix_class_sessions_room_id",
                schema: "academic",
                table: "class_sessions",
                column: "room_id");

            migrationBuilder.CreateIndex(
                name: "ix_classes_branch_id_status",
                schema: "academic",
                table: "classes",
                columns: new[] { "branch_id", "status" });

            migrationBuilder.CreateIndex(
                name: "ix_classes_code",
                schema: "academic",
                table: "classes",
                column: "code",
                unique: true,
                filter: "is_deleted = false");

            migrationBuilder.CreateIndex(
                name: "ix_classes_course_id",
                schema: "academic",
                table: "classes",
                column: "course_id");

            migrationBuilder.CreateIndex(
                name: "ix_classes_curriculum_id",
                schema: "academic",
                table: "classes",
                column: "curriculum_id");

            migrationBuilder.CreateIndex(
                name: "ix_classes_previous_class_id",
                schema: "academic",
                table: "classes",
                column: "previous_class_id");

            migrationBuilder.CreateIndex(
                name: "ix_classes_room_id",
                schema: "academic",
                table: "classes",
                column: "room_id");

            migrationBuilder.CreateIndex(
                name: "ix_room_issues_room_id_status",
                schema: "academic",
                table: "room_issues",
                columns: new[] { "room_id", "status" });

            migrationBuilder.CreateIndex(
                name: "ix_rooms_branch_id_name",
                schema: "academic",
                table: "rooms",
                columns: new[] { "branch_id", "name" },
                unique: true,
                filter: "is_deleted = false");

            migrationBuilder.CreateIndex(
                name: "ix_session_support_entries_class_session_id",
                schema: "academic",
                table: "session_support_entries",
                column: "class_session_id");

            migrationBuilder.CreateIndex(
                name: "ix_teacher_attendances_teacher_id",
                schema: "academic",
                table: "teacher_attendances",
                column: "teacher_id");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "attendance_records",
                schema: "academic");

            migrationBuilder.DropTable(
                name: "room_issues",
                schema: "academic");

            migrationBuilder.DropTable(
                name: "session_support_entries",
                schema: "academic");

            migrationBuilder.DropTable(
                name: "teacher_attendances",
                schema: "academic");

            migrationBuilder.DropTable(
                name: "class_sessions",
                schema: "academic");

            migrationBuilder.DropTable(
                name: "classes",
                schema: "academic");

            migrationBuilder.DropTable(
                name: "rooms",
                schema: "academic");
        }
    }
}
