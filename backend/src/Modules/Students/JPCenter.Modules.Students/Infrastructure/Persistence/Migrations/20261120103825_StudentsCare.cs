using System;
using System.Collections.Generic;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace JPCenter.Modules.Students.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class StudentsCare : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "care_cases",
                schema: "students",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    student_id = table.Column<Guid>(type: "uuid", nullable: false),
                    class_id = table.Column<Guid>(type: "uuid", nullable: false),
                    branch_id = table.Column<Guid>(type: "uuid", nullable: false),
                    risk_level = table.Column<string>(type: "text", nullable: false),
                    flags = table.Column<List<string>>(type: "text[]", nullable: false),
                    assignee_id = table.Column<Guid>(type: "uuid", nullable: true),
                    status = table.Column<string>(type: "text", nullable: false),
                    next_at = table.Column<DateOnly>(type: "date", nullable: true),
                    detected_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    created_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    created_by = table.Column<Guid>(type: "uuid", nullable: true),
                    updated_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    updated_by = table.Column<Guid>(type: "uuid", nullable: true),
                    xmin = table.Column<uint>(type: "xid", rowVersion: true, nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_care_cases", x => x.id);
                    table.ForeignKey(
                        name: "fk_care_cases_students_student_id",
                        column: x => x.student_id,
                        principalSchema: "students",
                        principalTable: "students",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "care_logs",
                schema: "students",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    student_id = table.Column<Guid>(type: "uuid", nullable: false),
                    class_id = table.Column<Guid>(type: "uuid", nullable: false),
                    branch_id = table.Column<Guid>(type: "uuid", nullable: false),
                    channel = table.Column<string>(type: "text", nullable: false),
                    note = table.Column<string>(type: "character varying(2000)", maxLength: 2000, nullable: false),
                    by = table.Column<Guid>(type: "uuid", nullable: false),
                    by_name = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: false),
                    at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_care_logs", x => x.id);
                    table.ForeignKey(
                        name: "fk_care_logs_students_student_id",
                        column: x => x.student_id,
                        principalSchema: "students",
                        principalTable: "students",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateIndex(
                name: "ix_care_cases_assignee_id",
                schema: "students",
                table: "care_cases",
                column: "assignee_id");

            migrationBuilder.CreateIndex(
                name: "ix_care_cases_branch_id_status",
                schema: "students",
                table: "care_cases",
                columns: new[] { "branch_id", "status" });

            migrationBuilder.CreateIndex(
                name: "ix_care_cases_class_id_student_id",
                schema: "students",
                table: "care_cases",
                columns: new[] { "class_id", "student_id" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "ix_care_cases_student_id",
                schema: "students",
                table: "care_cases",
                column: "student_id");

            migrationBuilder.CreateIndex(
                name: "ix_care_logs_class_id_student_id",
                schema: "students",
                table: "care_logs",
                columns: new[] { "class_id", "student_id" });

            migrationBuilder.CreateIndex(
                name: "ix_care_logs_student_id",
                schema: "students",
                table: "care_logs",
                column: "student_id");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "care_cases",
                schema: "students");

            migrationBuilder.DropTable(
                name: "care_logs",
                schema: "students");
        }
    }
}
