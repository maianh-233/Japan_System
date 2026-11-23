using System;
using System.Collections.Generic;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace JPCenter.Modules.Hr.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class HrTimesheetsRequestsRecruitment : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "hr_requests",
                schema: "hr",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    code = table.Column<string>(type: "character varying(30)", maxLength: 30, nullable: false),
                    type = table.Column<string>(type: "text", nullable: false),
                    employee_id = table.Column<Guid>(type: "uuid", nullable: false),
                    branch_id = table.Column<Guid>(type: "uuid", nullable: false),
                    from = table.Column<DateOnly>(type: "date", nullable: false),
                    to = table.Column<DateOnly>(type: "date", nullable: false),
                    days = table.Column<decimal>(type: "numeric(18,2)", precision: 18, scale: 2, nullable: true),
                    paid_leave = table.Column<bool>(type: "boolean", nullable: true),
                    swap_my_shift_id = table.Column<Guid>(type: "uuid", nullable: true),
                    swap_with_employee_id = table.Column<Guid>(type: "uuid", nullable: true),
                    swap_their_shift_id = table.Column<Guid>(type: "uuid", nullable: true),
                    amount = table.Column<decimal>(type: "numeric(18,0)", precision: 18, scale: 0, nullable: true),
                    title = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: true),
                    reason = table.Column<string>(type: "character varying(2000)", maxLength: 2000, nullable: false),
                    status = table.Column<string>(type: "text", nullable: false),
                    requested_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    created_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    created_by = table.Column<Guid>(type: "uuid", nullable: true),
                    updated_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    updated_by = table.Column<Guid>(type: "uuid", nullable: true),
                    xmin = table.Column<uint>(type: "xid", rowVersion: true, nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_hr_requests", x => x.id);
                    table.ForeignKey(
                        name: "fk_hr_requests_employees_employee_id",
                        column: x => x.employee_id,
                        principalSchema: "hr",
                        principalTable: "employees",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "fk_hr_requests_employees_swap_with_employee_id",
                        column: x => x.swap_with_employee_id,
                        principalSchema: "hr",
                        principalTable: "employees",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "jobs",
                schema: "hr",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    title = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: false),
                    branch_id = table.Column<Guid>(type: "uuid", nullable: false),
                    department = table.Column<string>(type: "text", nullable: false),
                    kind = table.Column<string>(type: "text", nullable: false),
                    quantity = table.Column<int>(type: "integer", nullable: false),
                    salary_range = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: true),
                    status = table.Column<string>(type: "text", nullable: false),
                    posted_at = table.Column<DateOnly>(type: "date", nullable: false),
                    deadline = table.Column<DateOnly>(type: "date", nullable: false),
                    channels = table.Column<List<string>>(type: "text[]", nullable: false),
                    description = table.Column<string>(type: "character varying(4000)", maxLength: 4000, nullable: true),
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
                    table.PrimaryKey("pk_jobs", x => x.id);
                });

            migrationBuilder.CreateTable(
                name: "time_records",
                schema: "hr",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    employee_id = table.Column<Guid>(type: "uuid", nullable: false),
                    branch_id = table.Column<Guid>(type: "uuid", nullable: false),
                    date = table.Column<DateOnly>(type: "date", nullable: false),
                    check_in = table.Column<TimeOnly>(type: "time without time zone", nullable: true),
                    check_out = table.Column<TimeOnly>(type: "time without time zone", nullable: true),
                    edited_by = table.Column<Guid>(type: "uuid", nullable: true),
                    edited_by_name = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: true),
                    edit_reason = table.Column<string>(type: "character varying(500)", maxLength: 500, nullable: true),
                    created_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    created_by = table.Column<Guid>(type: "uuid", nullable: true),
                    updated_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    updated_by = table.Column<Guid>(type: "uuid", nullable: true),
                    xmin = table.Column<uint>(type: "xid", rowVersion: true, nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_time_records", x => x.id);
                    table.ForeignKey(
                        name: "fk_time_records_employees_employee_id",
                        column: x => x.employee_id,
                        principalSchema: "hr",
                        principalTable: "employees",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "hr_request_steps",
                schema: "hr",
                columns: table => new
                {
                    order = table.Column<int>(type: "integer", nullable: false),
                    hr_request_id = table.Column<Guid>(type: "uuid", nullable: false),
                    role = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: false),
                    approver_id = table.Column<Guid>(type: "uuid", nullable: true),
                    approver_name = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: true),
                    decision = table.Column<string>(type: "text", nullable: true),
                    decided_by_id = table.Column<Guid>(type: "uuid", nullable: true),
                    decided_by_name = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: true),
                    on_behalf = table.Column<bool>(type: "boolean", nullable: false),
                    at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    comment = table.Column<string>(type: "character varying(1000)", maxLength: 1000, nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_hr_request_steps", x => new { x.hr_request_id, x.order });
                    table.ForeignKey(
                        name: "fk_hr_request_steps_hr_requests_hr_request_id",
                        column: x => x.hr_request_id,
                        principalSchema: "hr",
                        principalTable: "hr_requests",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "candidates",
                schema: "hr",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    job_id = table.Column<Guid>(type: "uuid", nullable: false),
                    full_name = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: false),
                    phone = table.Column<string>(type: "character varying(30)", maxLength: 30, nullable: false),
                    email = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: false),
                    source = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: false),
                    stage = table.Column<string>(type: "text", nullable: false),
                    applied_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    cv_key = table.Column<string>(type: "character varying(500)", maxLength: 500, nullable: true),
                    cv_name = table.Column<string>(type: "character varying(300)", maxLength: 300, nullable: true),
                    japanese = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: true),
                    expected_salary = table.Column<decimal>(type: "numeric(18,0)", precision: 18, scale: 0, nullable: true),
                    interview_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    interviewer_id = table.Column<Guid>(type: "uuid", nullable: true),
                    interview_mode = table.Column<string>(type: "text", nullable: true),
                    interview_score = table.Column<decimal>(type: "numeric(4,2)", precision: 4, scale: 2, nullable: true),
                    interview_note = table.Column<string>(type: "character varying(2000)", maxLength: 2000, nullable: true),
                    offer_salary = table.Column<decimal>(type: "numeric(18,0)", precision: 18, scale: 0, nullable: true),
                    offer_start_date = table.Column<DateOnly>(type: "date", nullable: true),
                    offer_sent_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    offer_accepted = table.Column<bool>(type: "boolean", nullable: true),
                    reject_reason = table.Column<string>(type: "character varying(500)", maxLength: 500, nullable: true),
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
                    table.PrimaryKey("pk_candidates", x => x.id);
                    table.ForeignKey(
                        name: "fk_candidates_jobs_job_id",
                        column: x => x.job_id,
                        principalSchema: "hr",
                        principalTable: "jobs",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "candidate_notes",
                schema: "hr",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    text = table.Column<string>(type: "character varying(2000)", maxLength: 2000, nullable: false),
                    by = table.Column<Guid>(type: "uuid", nullable: false),
                    by_name = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: false),
                    at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    candidate_id = table.Column<Guid>(type: "uuid", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_candidate_notes", x => x.id);
                    table.ForeignKey(
                        name: "fk_candidate_notes_candidates_candidate_id",
                        column: x => x.candidate_id,
                        principalSchema: "hr",
                        principalTable: "candidates",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "onboardings",
                schema: "hr",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    candidate_id = table.Column<Guid>(type: "uuid", nullable: true),
                    full_name = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: false),
                    phone = table.Column<string>(type: "character varying(30)", maxLength: 30, nullable: false),
                    email = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: false),
                    position = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: false),
                    department = table.Column<string>(type: "text", nullable: false),
                    kind = table.Column<string>(type: "text", nullable: false),
                    branch_id = table.Column<Guid>(type: "uuid", nullable: false),
                    start_date = table.Column<DateOnly>(type: "date", nullable: false),
                    mentor_id = table.Column<Guid>(type: "uuid", nullable: false),
                    employee_id = table.Column<Guid>(type: "uuid", nullable: true),
                    created_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    created_by = table.Column<Guid>(type: "uuid", nullable: true),
                    updated_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    updated_by = table.Column<Guid>(type: "uuid", nullable: true),
                    xmin = table.Column<uint>(type: "xid", rowVersion: true, nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_onboardings", x => x.id);
                    table.ForeignKey(
                        name: "fk_onboardings_candidates_candidate_id",
                        column: x => x.candidate_id,
                        principalSchema: "hr",
                        principalTable: "candidates",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "fk_onboardings_employees_employee_id",
                        column: x => x.employee_id,
                        principalSchema: "hr",
                        principalTable: "employees",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "fk_onboardings_employees_mentor_id",
                        column: x => x.mentor_id,
                        principalSchema: "hr",
                        principalTable: "employees",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "onboarding_items",
                schema: "hr",
                columns: table => new
                {
                    key = table.Column<string>(type: "character varying(50)", maxLength: 50, nullable: false),
                    onboarding_id = table.Column<Guid>(type: "uuid", nullable: false),
                    group = table.Column<string>(type: "text", nullable: false),
                    label = table.Column<string>(type: "character varying(300)", maxLength: 300, nullable: false),
                    done = table.Column<bool>(type: "boolean", nullable: false),
                    done_by = table.Column<Guid>(type: "uuid", nullable: true),
                    done_by_name = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: true),
                    done_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_onboarding_items", x => new { x.onboarding_id, x.key });
                    table.ForeignKey(
                        name: "fk_onboarding_items_onboardings_onboarding_id",
                        column: x => x.onboarding_id,
                        principalSchema: "hr",
                        principalTable: "onboardings",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "ix_candidate_notes_candidate_id",
                schema: "hr",
                table: "candidate_notes",
                column: "candidate_id");

            migrationBuilder.CreateIndex(
                name: "ix_candidates_interview_at",
                schema: "hr",
                table: "candidates",
                column: "interview_at");

            migrationBuilder.CreateIndex(
                name: "ix_candidates_job_id_stage",
                schema: "hr",
                table: "candidates",
                columns: new[] { "job_id", "stage" });

            migrationBuilder.CreateIndex(
                name: "ix_hr_request_steps_approver_id_decision",
                schema: "hr",
                table: "hr_request_steps",
                columns: new[] { "approver_id", "decision" });

            migrationBuilder.CreateIndex(
                name: "ix_hr_requests_branch_id_status",
                schema: "hr",
                table: "hr_requests",
                columns: new[] { "branch_id", "status" });

            migrationBuilder.CreateIndex(
                name: "ix_hr_requests_code",
                schema: "hr",
                table: "hr_requests",
                column: "code",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "ix_hr_requests_employee_id_status",
                schema: "hr",
                table: "hr_requests",
                columns: new[] { "employee_id", "status" });

            migrationBuilder.CreateIndex(
                name: "ix_hr_requests_swap_with_employee_id",
                schema: "hr",
                table: "hr_requests",
                column: "swap_with_employee_id");

            migrationBuilder.CreateIndex(
                name: "ix_jobs_branch_id_status",
                schema: "hr",
                table: "jobs",
                columns: new[] { "branch_id", "status" });

            migrationBuilder.CreateIndex(
                name: "ix_onboardings_branch_id_start_date",
                schema: "hr",
                table: "onboardings",
                columns: new[] { "branch_id", "start_date" });

            migrationBuilder.CreateIndex(
                name: "ix_onboardings_candidate_id",
                schema: "hr",
                table: "onboardings",
                column: "candidate_id",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "ix_onboardings_employee_id",
                schema: "hr",
                table: "onboardings",
                column: "employee_id",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "ix_onboardings_mentor_id",
                schema: "hr",
                table: "onboardings",
                column: "mentor_id");

            migrationBuilder.CreateIndex(
                name: "ix_time_records_branch_id_date",
                schema: "hr",
                table: "time_records",
                columns: new[] { "branch_id", "date" });

            migrationBuilder.CreateIndex(
                name: "ix_time_records_employee_id_date",
                schema: "hr",
                table: "time_records",
                columns: new[] { "employee_id", "date" },
                unique: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "candidate_notes",
                schema: "hr");

            migrationBuilder.DropTable(
                name: "hr_request_steps",
                schema: "hr");

            migrationBuilder.DropTable(
                name: "onboarding_items",
                schema: "hr");

            migrationBuilder.DropTable(
                name: "time_records",
                schema: "hr");

            migrationBuilder.DropTable(
                name: "hr_requests",
                schema: "hr");

            migrationBuilder.DropTable(
                name: "onboardings",
                schema: "hr");

            migrationBuilder.DropTable(
                name: "candidates",
                schema: "hr");

            migrationBuilder.DropTable(
                name: "jobs",
                schema: "hr");
        }
    }
}
