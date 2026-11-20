using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace JPCenter.Modules.Academic.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class AcademicRequestsPromotions : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "leave_requests",
                schema: "academic",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    student_id = table.Column<Guid>(type: "uuid", nullable: false),
                    class_id = table.Column<Guid>(type: "uuid", nullable: false),
                    session_id = table.Column<Guid>(type: "uuid", nullable: false),
                    branch_id = table.Column<Guid>(type: "uuid", nullable: false),
                    reason_type = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: false),
                    reason = table.Column<string>(type: "character varying(1000)", maxLength: 1000, nullable: false),
                    by = table.Column<string>(type: "text", nullable: false),
                    by_account_id = table.Column<Guid>(type: "uuid", nullable: false),
                    by_name = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: false),
                    status = table.Column<string>(type: "text", nullable: false),
                    requested_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    decided_by = table.Column<Guid>(type: "uuid", nullable: true),
                    decided_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    makeup_session_id = table.Column<Guid>(type: "uuid", nullable: true),
                    makeup_result = table.Column<string>(type: "text", nullable: true),
                    note = table.Column<string>(type: "character varying(1000)", maxLength: 1000, nullable: true),
                    created_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    created_by = table.Column<Guid>(type: "uuid", nullable: true),
                    updated_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    updated_by = table.Column<Guid>(type: "uuid", nullable: true),
                    xmin = table.Column<uint>(type: "xid", rowVersion: true, nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_leave_requests", x => x.id);
                    table.ForeignKey(
                        name: "fk_leave_requests_classes_class_id",
                        column: x => x.class_id,
                        principalSchema: "academic",
                        principalTable: "classes",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "promotion_candidates",
                schema: "academic",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    student_id = table.Column<Guid>(type: "uuid", nullable: false),
                    branch_id = table.Column<Guid>(type: "uuid", nullable: false),
                    from_class_id = table.Column<Guid>(type: "uuid", nullable: false),
                    to_class_id = table.Column<Guid>(type: "uuid", nullable: true),
                    seat_held = table.Column<bool>(type: "boolean", nullable: false),
                    hold_until = table.Column<DateOnly>(type: "date", nullable: true),
                    status = table.Column<string>(type: "text", nullable: false),
                    source = table.Column<string>(type: "text", nullable: false),
                    handed_over_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    sale_id = table.Column<Guid>(type: "uuid", nullable: true),
                    interest_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    interest_by = table.Column<string>(type: "text", nullable: true),
                    decline_reason = table.Column<string>(type: "character varying(500)", maxLength: 500, nullable: true),
                    enrollment_id = table.Column<Guid>(type: "uuid", nullable: true),
                    confirmed_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    confirmed_by = table.Column<Guid>(type: "uuid", nullable: true),
                    created_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    created_by = table.Column<Guid>(type: "uuid", nullable: true),
                    updated_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    updated_by = table.Column<Guid>(type: "uuid", nullable: true),
                    xmin = table.Column<uint>(type: "xid", rowVersion: true, nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_promotion_candidates", x => x.id);
                    table.ForeignKey(
                        name: "fk_promotion_candidates_classes_from_class_id",
                        column: x => x.from_class_id,
                        principalSchema: "academic",
                        principalTable: "classes",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "fk_promotion_candidates_classes_to_class_id",
                        column: x => x.to_class_id,
                        principalSchema: "academic",
                        principalTable: "classes",
                        principalColumn: "id",
                        onDelete: ReferentialAction.SetNull);
                });

            migrationBuilder.CreateTable(
                name: "session_cancel_requests",
                schema: "academic",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    session_id = table.Column<Guid>(type: "uuid", nullable: false),
                    class_id = table.Column<Guid>(type: "uuid", nullable: false),
                    branch_id = table.Column<Guid>(type: "uuid", nullable: false),
                    teacher_id = table.Column<Guid>(type: "uuid", nullable: false),
                    reason_type = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: false),
                    reason = table.Column<string>(type: "character varying(1000)", maxLength: 1000, nullable: false),
                    proposed_makeup_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    status = table.Column<string>(type: "text", nullable: false),
                    requested_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    decided_by = table.Column<Guid>(type: "uuid", nullable: true),
                    decided_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    note = table.Column<string>(type: "character varying(1000)", maxLength: 1000, nullable: true),
                    created_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    created_by = table.Column<Guid>(type: "uuid", nullable: true),
                    updated_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    updated_by = table.Column<Guid>(type: "uuid", nullable: true),
                    xmin = table.Column<uint>(type: "xid", rowVersion: true, nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_session_cancel_requests", x => x.id);
                    table.ForeignKey(
                        name: "fk_session_cancel_requests_classes_class_id",
                        column: x => x.class_id,
                        principalSchema: "academic",
                        principalTable: "classes",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "student_evaluations",
                schema: "academic",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    class_id = table.Column<Guid>(type: "uuid", nullable: false),
                    student_id = table.Column<Guid>(type: "uuid", nullable: false),
                    teacher_id = table.Column<Guid>(type: "uuid", nullable: false),
                    vocab = table.Column<int>(type: "integer", nullable: false),
                    grammar = table.Column<int>(type: "integer", nullable: false),
                    reading = table.Column<int>(type: "integer", nullable: false),
                    listening = table.Column<int>(type: "integer", nullable: false),
                    speaking = table.Column<int>(type: "integer", nullable: false),
                    attitude = table.Column<int>(type: "integer", nullable: false),
                    strengths = table.Column<string>(type: "character varying(2000)", maxLength: 2000, nullable: true),
                    weaknesses = table.Column<string>(type: "character varying(2000)", maxLength: 2000, nullable: true),
                    recommendation = table.Column<string>(type: "text", nullable: false),
                    advice = table.Column<string>(type: "character varying(2000)", maxLength: 2000, nullable: true),
                    created_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    created_by = table.Column<Guid>(type: "uuid", nullable: true),
                    updated_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    updated_by = table.Column<Guid>(type: "uuid", nullable: true),
                    xmin = table.Column<uint>(type: "xid", rowVersion: true, nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_student_evaluations", x => x.id);
                    table.ForeignKey(
                        name: "fk_student_evaluations_classes_class_id",
                        column: x => x.class_id,
                        principalSchema: "academic",
                        principalTable: "classes",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "promotion_notes",
                schema: "academic",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    text = table.Column<string>(type: "character varying(2000)", maxLength: 2000, nullable: false),
                    channel = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: true),
                    by = table.Column<Guid>(type: "uuid", nullable: false),
                    by_name = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: false),
                    at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    promotion_candidate_id = table.Column<Guid>(type: "uuid", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_promotion_notes", x => x.id);
                    table.ForeignKey(
                        name: "fk_promotion_notes_promotion_candidates_promotion_candidate_id",
                        column: x => x.promotion_candidate_id,
                        principalSchema: "academic",
                        principalTable: "promotion_candidates",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "substitute_invites",
                schema: "academic",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    session_id = table.Column<Guid>(type: "uuid", nullable: false),
                    class_id = table.Column<Guid>(type: "uuid", nullable: false),
                    branch_id = table.Column<Guid>(type: "uuid", nullable: false),
                    cancel_request_id = table.Column<Guid>(type: "uuid", nullable: true),
                    from_teacher_id = table.Column<Guid>(type: "uuid", nullable: false),
                    to_teacher_id = table.Column<Guid>(type: "uuid", nullable: false),
                    reason = table.Column<string>(type: "character varying(1000)", maxLength: 1000, nullable: false),
                    invited_by = table.Column<Guid>(type: "uuid", nullable: false),
                    invited_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    respond_by = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    status = table.Column<string>(type: "text", nullable: false),
                    responded_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    decline_reason = table.Column<string>(type: "character varying(500)", maxLength: 500, nullable: true),
                    created_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    created_by = table.Column<Guid>(type: "uuid", nullable: true),
                    updated_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    updated_by = table.Column<Guid>(type: "uuid", nullable: true),
                    xmin = table.Column<uint>(type: "xid", rowVersion: true, nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_substitute_invites", x => x.id);
                    table.ForeignKey(
                        name: "fk_substitute_invites_classes_class_id",
                        column: x => x.class_id,
                        principalSchema: "academic",
                        principalTable: "classes",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "fk_substitute_invites_session_cancel_requests_cancel_request_id",
                        column: x => x.cancel_request_id,
                        principalSchema: "academic",
                        principalTable: "session_cancel_requests",
                        principalColumn: "id",
                        onDelete: ReferentialAction.SetNull);
                });

            migrationBuilder.CreateIndex(
                name: "ix_leave_requests_branch_id_status",
                schema: "academic",
                table: "leave_requests",
                columns: new[] { "branch_id", "status" });

            migrationBuilder.CreateIndex(
                name: "ix_leave_requests_class_id",
                schema: "academic",
                table: "leave_requests",
                column: "class_id");

            migrationBuilder.CreateIndex(
                name: "ix_leave_requests_makeup_session_id",
                schema: "academic",
                table: "leave_requests",
                column: "makeup_session_id");

            migrationBuilder.CreateIndex(
                name: "ix_leave_requests_session_id_student_id",
                schema: "academic",
                table: "leave_requests",
                columns: new[] { "session_id", "student_id" });

            migrationBuilder.CreateIndex(
                name: "ix_leave_requests_student_id_status",
                schema: "academic",
                table: "leave_requests",
                columns: new[] { "student_id", "status" });

            migrationBuilder.CreateIndex(
                name: "ix_promotion_candidates_from_class_id_student_id",
                schema: "academic",
                table: "promotion_candidates",
                columns: new[] { "from_class_id", "student_id" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "ix_promotion_candidates_sale_id_status",
                schema: "academic",
                table: "promotion_candidates",
                columns: new[] { "sale_id", "status" });

            migrationBuilder.CreateIndex(
                name: "ix_promotion_candidates_to_class_id_seat_held",
                schema: "academic",
                table: "promotion_candidates",
                columns: new[] { "to_class_id", "seat_held" });

            migrationBuilder.CreateIndex(
                name: "ix_promotion_notes_promotion_candidate_id",
                schema: "academic",
                table: "promotion_notes",
                column: "promotion_candidate_id");

            migrationBuilder.CreateIndex(
                name: "ix_session_cancel_requests_branch_id_status",
                schema: "academic",
                table: "session_cancel_requests",
                columns: new[] { "branch_id", "status" });

            migrationBuilder.CreateIndex(
                name: "ix_session_cancel_requests_class_id",
                schema: "academic",
                table: "session_cancel_requests",
                column: "class_id");

            migrationBuilder.CreateIndex(
                name: "ix_session_cancel_requests_session_id_status",
                schema: "academic",
                table: "session_cancel_requests",
                columns: new[] { "session_id", "status" });

            migrationBuilder.CreateIndex(
                name: "ix_session_cancel_requests_teacher_id_status",
                schema: "academic",
                table: "session_cancel_requests",
                columns: new[] { "teacher_id", "status" });

            migrationBuilder.CreateIndex(
                name: "ix_student_evaluations_class_id_student_id",
                schema: "academic",
                table: "student_evaluations",
                columns: new[] { "class_id", "student_id" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "ix_student_evaluations_student_id",
                schema: "academic",
                table: "student_evaluations",
                column: "student_id");

            migrationBuilder.CreateIndex(
                name: "ix_substitute_invites_cancel_request_id",
                schema: "academic",
                table: "substitute_invites",
                column: "cancel_request_id");

            migrationBuilder.CreateIndex(
                name: "ix_substitute_invites_class_id",
                schema: "academic",
                table: "substitute_invites",
                column: "class_id");

            migrationBuilder.CreateIndex(
                name: "ix_substitute_invites_session_id_status",
                schema: "academic",
                table: "substitute_invites",
                columns: new[] { "session_id", "status" });

            migrationBuilder.CreateIndex(
                name: "ix_substitute_invites_to_teacher_id_status",
                schema: "academic",
                table: "substitute_invites",
                columns: new[] { "to_teacher_id", "status" });
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "leave_requests",
                schema: "academic");

            migrationBuilder.DropTable(
                name: "promotion_notes",
                schema: "academic");

            migrationBuilder.DropTable(
                name: "student_evaluations",
                schema: "academic");

            migrationBuilder.DropTable(
                name: "substitute_invites",
                schema: "academic");

            migrationBuilder.DropTable(
                name: "promotion_candidates",
                schema: "academic");

            migrationBuilder.DropTable(
                name: "session_cancel_requests",
                schema: "academic");
        }
    }
}
