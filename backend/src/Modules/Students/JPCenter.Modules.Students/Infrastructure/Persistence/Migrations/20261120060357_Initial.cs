using System;
using System.Collections.Generic;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace JPCenter.Modules.Students.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class Initial : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.EnsureSchema(
                name: "students");

            migrationBuilder.CreateTable(
                name: "outbox_messages",
                schema: "students",
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
                name: "students",
                schema: "students",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    code = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: false),
                    full_name = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: false),
                    gender = table.Column<string>(type: "text", nullable: true),
                    dob = table.Column<DateOnly>(type: "date", nullable: true),
                    phone = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: true),
                    email = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: true),
                    address = table.Column<string>(type: "character varying(500)", maxLength: 500, nullable: true),
                    note = table.Column<string>(type: "character varying(2000)", maxLength: 2000, nullable: true),
                    branch_id = table.Column<Guid>(type: "uuid", nullable: false),
                    joined_at = table.Column<DateOnly>(type: "date", nullable: false),
                    hidden = table.Column<bool>(type: "boolean", nullable: false),
                    hidden_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
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
                    table.PrimaryKey("pk_students", x => x.id);
                });

            migrationBuilder.CreateTable(
                name: "enrollments",
                schema: "students",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    student_id = table.Column<Guid>(type: "uuid", nullable: false),
                    class_id = table.Column<Guid>(type: "uuid", nullable: false),
                    branch_id = table.Column<Guid>(type: "uuid", nullable: false),
                    status = table.Column<string>(type: "text", nullable: false),
                    enrolled_at = table.Column<DateOnly>(type: "date", nullable: false),
                    ended_at = table.Column<DateOnly>(type: "date", nullable: true),
                    note = table.Column<string>(type: "character varying(1000)", maxLength: 1000, nullable: true),
                    reserved_until = table.Column<DateOnly>(type: "date", nullable: true),
                    transferred_to_id = table.Column<Guid>(type: "uuid", nullable: true),
                    transferred_from_id = table.Column<Guid>(type: "uuid", nullable: true),
                    promotion_candidate_id = table.Column<Guid>(type: "uuid", nullable: true),
                    created_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    created_by = table.Column<Guid>(type: "uuid", nullable: true),
                    updated_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    updated_by = table.Column<Guid>(type: "uuid", nullable: true),
                    xmin = table.Column<uint>(type: "xid", rowVersion: true, nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_enrollments", x => x.id);
                    table.ForeignKey(
                        name: "fk_enrollments_enrollments_transferred_from_id",
                        column: x => x.transferred_from_id,
                        principalSchema: "students",
                        principalTable: "enrollments",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "fk_enrollments_enrollments_transferred_to_id",
                        column: x => x.transferred_to_id,
                        principalSchema: "students",
                        principalTable: "enrollments",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "fk_enrollments_students_student_id",
                        column: x => x.student_id,
                        principalSchema: "students",
                        principalTable: "students",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "certificates",
                schema: "students",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    code = table.Column<string>(type: "character varying(60)", maxLength: 60, nullable: false),
                    issued_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    sent_to = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: false),
                    sent_by = table.Column<Guid>(type: "uuid", nullable: false),
                    send_count = table.Column<int>(type: "integer", nullable: false),
                    exception = table.Column<string>(type: "character varying(500)", maxLength: 500, nullable: true),
                    file_key = table.Column<string>(type: "character varying(300)", maxLength: 300, nullable: true),
                    id1 = table.Column<Guid>(type: "uuid", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_certificates", x => x.id);
                    table.ForeignKey(
                        name: "fk_certificates_enrollments_id",
                        column: x => x.id1,
                        principalSchema: "students",
                        principalTable: "enrollments",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "ix_certificates_code",
                schema: "students",
                table: "certificates",
                column: "code",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "ix_certificates_id",
                schema: "students",
                table: "certificates",
                column: "id1",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "ix_enrollments_class_id_status",
                schema: "students",
                table: "enrollments",
                columns: new[] { "class_id", "status" });

            migrationBuilder.CreateIndex(
                name: "ix_enrollments_student_id_status",
                schema: "students",
                table: "enrollments",
                columns: new[] { "student_id", "status" });

            migrationBuilder.CreateIndex(
                name: "ix_enrollments_transferred_from_id",
                schema: "students",
                table: "enrollments",
                column: "transferred_from_id");

            migrationBuilder.CreateIndex(
                name: "ix_enrollments_transferred_to_id",
                schema: "students",
                table: "enrollments",
                column: "transferred_to_id");

            migrationBuilder.CreateIndex(
                name: "ix_outbox_messages_next_attempt_at",
                schema: "students",
                table: "outbox_messages",
                column: "next_attempt_at",
                filter: "processed_at IS NULL AND failed_at IS NULL");

            migrationBuilder.CreateIndex(
                name: "ix_students_branch_id_hidden",
                schema: "students",
                table: "students",
                columns: new[] { "branch_id", "hidden" });

            migrationBuilder.CreateIndex(
                name: "ix_students_code",
                schema: "students",
                table: "students",
                column: "code",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "ix_students_phone",
                schema: "students",
                table: "students",
                column: "phone");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "certificates",
                schema: "students");

            migrationBuilder.DropTable(
                name: "outbox_messages",
                schema: "students");

            migrationBuilder.DropTable(
                name: "enrollments",
                schema: "students");

            migrationBuilder.DropTable(
                name: "students",
                schema: "students");
        }
    }
}
