using System;
using System.Collections.Generic;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace JPCenter.Modules.Students.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class StudentsPrograms : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "abroad_cases",
                schema: "students",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    student_id = table.Column<Guid>(type: "uuid", nullable: false),
                    branch_id = table.Column<Guid>(type: "uuid", nullable: false),
                    program = table.Column<string>(type: "text", nullable: false),
                    partner = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: false),
                    destination = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: false),
                    job = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: true),
                    intake = table.Column<string>(type: "character varying(7)", maxLength: 7, nullable: false),
                    jlpt_required = table.Column<string>(type: "text", nullable: true),
                    stage = table.Column<string>(type: "text", nullable: false),
                    cancelled_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    cancel_reason = table.Column<string>(type: "character varying(500)", maxLength: 500, nullable: true),
                    interview_date = table.Column<DateOnly>(type: "date", nullable: true),
                    interview_result = table.Column<string>(type: "text", nullable: true),
                    coe_submitted_at = table.Column<DateOnly>(type: "date", nullable: true),
                    coe_granted_at = table.Column<DateOnly>(type: "date", nullable: true),
                    visa_applied_at = table.Column<DateOnly>(type: "date", nullable: true),
                    visa_issued_at = table.Column<DateOnly>(type: "date", nullable: true),
                    flight_date = table.Column<DateOnly>(type: "date", nullable: true),
                    fee_total = table.Column<decimal>(type: "numeric(18,0)", precision: 18, scale: 0, nullable: false),
                    fee_paid = table.Column<decimal>(type: "numeric(18,0)", precision: 18, scale: 0, nullable: false),
                    assignee_id = table.Column<Guid>(type: "uuid", nullable: false),
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
                    table.PrimaryKey("pk_abroad_cases", x => x.id);
                    table.ForeignKey(
                        name: "fk_abroad_cases_students_student_id",
                        column: x => x.student_id,
                        principalSchema: "students",
                        principalTable: "students",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "jlpt_sessions",
                schema: "students",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    label = table.Column<string>(type: "character varying(50)", maxLength: 50, nullable: false),
                    exam_date = table.Column<DateOnly>(type: "date", nullable: false),
                    registration_close = table.Column<DateOnly>(type: "date", nullable: false),
                    result_date = table.Column<DateOnly>(type: "date", nullable: false),
                    fees = table.Column<string>(type: "jsonb", nullable: false),
                    venues = table.Column<List<string>>(type: "text[]", nullable: false),
                    created_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    created_by = table.Column<Guid>(type: "uuid", nullable: true),
                    updated_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    updated_by = table.Column<Guid>(type: "uuid", nullable: true),
                    xmin = table.Column<uint>(type: "xid", rowVersion: true, nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_jlpt_sessions", x => x.id);
                });

            migrationBuilder.CreateTable(
                name: "abroad_documents",
                schema: "students",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    name = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: false),
                    state = table.Column<string>(type: "text", nullable: false),
                    abroad_case_id = table.Column<Guid>(type: "uuid", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_abroad_documents", x => x.id);
                    table.ForeignKey(
                        name: "fk_abroad_documents_abroad_cases_abroad_case_id",
                        column: x => x.abroad_case_id,
                        principalSchema: "students",
                        principalTable: "abroad_cases",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "abroad_notes",
                schema: "students",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    text = table.Column<string>(type: "character varying(1000)", maxLength: 1000, nullable: false),
                    by = table.Column<Guid>(type: "uuid", nullable: false),
                    by_name = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: false),
                    at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    abroad_case_id = table.Column<Guid>(type: "uuid", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_abroad_notes", x => x.id);
                    table.ForeignKey(
                        name: "fk_abroad_notes_abroad_cases_abroad_case_id",
                        column: x => x.abroad_case_id,
                        principalSchema: "students",
                        principalTable: "abroad_cases",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "jlpt_registrations",
                schema: "students",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    student_id = table.Column<Guid>(type: "uuid", nullable: false),
                    session_id = table.Column<Guid>(type: "uuid", nullable: false),
                    branch_id = table.Column<Guid>(type: "uuid", nullable: false),
                    level = table.Column<string>(type: "text", nullable: false),
                    venue = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: false),
                    status = table.Column<string>(type: "text", nullable: false),
                    doc_photo = table.Column<bool>(type: "boolean", nullable: false),
                    doc_id_card = table.Column<bool>(type: "boolean", nullable: false),
                    doc_form = table.Column<bool>(type: "boolean", nullable: false),
                    fee = table.Column<decimal>(type: "numeric(18,0)", precision: 18, scale: 0, nullable: false),
                    paid_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    admit_no = table.Column<string>(type: "character varying(50)", maxLength: 50, nullable: true),
                    scores = table.Column<List<int>>(type: "integer[]", nullable: false),
                    passed = table.Column<bool>(type: "boolean", nullable: true),
                    cert_no = table.Column<string>(type: "character varying(50)", maxLength: 50, nullable: true),
                    cert_received_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    cert_received_by = table.Column<Guid>(type: "uuid", nullable: true),
                    cert_handed_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    cert_handed_by = table.Column<Guid>(type: "uuid", nullable: true),
                    cert_handed_to = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: true),
                    note = table.Column<string>(type: "character varying(500)", maxLength: 500, nullable: true),
                    created_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    created_by = table.Column<Guid>(type: "uuid", nullable: true),
                    updated_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    updated_by = table.Column<Guid>(type: "uuid", nullable: true),
                    xmin = table.Column<uint>(type: "xid", rowVersion: true, nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_jlpt_registrations", x => x.id);
                    table.ForeignKey(
                        name: "fk_jlpt_registrations_jlpt_sessions_session_id",
                        column: x => x.session_id,
                        principalSchema: "students",
                        principalTable: "jlpt_sessions",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "fk_jlpt_registrations_students_student_id",
                        column: x => x.student_id,
                        principalSchema: "students",
                        principalTable: "students",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateIndex(
                name: "ix_abroad_cases_assignee_id",
                schema: "students",
                table: "abroad_cases",
                column: "assignee_id");

            migrationBuilder.CreateIndex(
                name: "ix_abroad_cases_branch_id_stage",
                schema: "students",
                table: "abroad_cases",
                columns: new[] { "branch_id", "stage" });

            migrationBuilder.CreateIndex(
                name: "ix_abroad_cases_student_id",
                schema: "students",
                table: "abroad_cases",
                column: "student_id");

            migrationBuilder.CreateIndex(
                name: "ix_abroad_documents_abroad_case_id",
                schema: "students",
                table: "abroad_documents",
                column: "abroad_case_id");

            migrationBuilder.CreateIndex(
                name: "ix_abroad_notes_abroad_case_id",
                schema: "students",
                table: "abroad_notes",
                column: "abroad_case_id");

            migrationBuilder.CreateIndex(
                name: "ix_jlpt_registrations_branch_id_status",
                schema: "students",
                table: "jlpt_registrations",
                columns: new[] { "branch_id", "status" });

            migrationBuilder.CreateIndex(
                name: "ix_jlpt_registrations_session_id_student_id",
                schema: "students",
                table: "jlpt_registrations",
                columns: new[] { "session_id", "student_id" },
                unique: true,
                filter: "status <> 'cancelled'");

            migrationBuilder.CreateIndex(
                name: "ix_jlpt_registrations_student_id",
                schema: "students",
                table: "jlpt_registrations",
                column: "student_id");

            migrationBuilder.CreateIndex(
                name: "ix_jlpt_sessions_exam_date",
                schema: "students",
                table: "jlpt_sessions",
                column: "exam_date",
                unique: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "abroad_documents",
                schema: "students");

            migrationBuilder.DropTable(
                name: "abroad_notes",
                schema: "students");

            migrationBuilder.DropTable(
                name: "jlpt_registrations",
                schema: "students");

            migrationBuilder.DropTable(
                name: "abroad_cases",
                schema: "students");

            migrationBuilder.DropTable(
                name: "jlpt_sessions",
                schema: "students");
        }
    }
}
