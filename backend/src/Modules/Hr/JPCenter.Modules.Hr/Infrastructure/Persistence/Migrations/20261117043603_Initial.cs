using System;
using System.Collections.Generic;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace JPCenter.Modules.Hr.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class Initial : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.EnsureSchema(
                name: "hr");

            migrationBuilder.CreateTable(
                name: "employees",
                schema: "hr",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    code = table.Column<string>(type: "text", nullable: false),
                    full_name = table.Column<string>(type: "text", nullable: false),
                    gender = table.Column<string>(type: "text", nullable: false),
                    dob = table.Column<DateOnly>(type: "date", nullable: true),
                    phone = table.Column<string>(type: "text", nullable: false),
                    email = table.Column<string>(type: "text", nullable: false),
                    branch_id = table.Column<Guid>(type: "uuid", nullable: false),
                    kind = table.Column<string>(type: "text", nullable: false),
                    department = table.Column<string>(type: "text", nullable: false),
                    position = table.Column<string>(type: "text", nullable: false),
                    shift_based = table.Column<bool>(type: "boolean", nullable: false),
                    status = table.Column<string>(type: "text", nullable: false),
                    joined_at = table.Column<DateOnly>(type: "date", nullable: false),
                    quit_at = table.Column<DateOnly>(type: "date", nullable: true),
                    manager_id = table.Column<Guid>(type: "uuid", nullable: true),
                    leave_balance = table.Column<decimal>(type: "numeric(5,1)", precision: 5, scale: 1, nullable: false),
                    has_account = table.Column<bool>(type: "boolean", nullable: false),
                    note = table.Column<string>(type: "text", nullable: true),
                    created_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    created_by = table.Column<Guid>(type: "uuid", nullable: true),
                    deleted_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    deleted_by = table.Column<Guid>(type: "uuid", nullable: true),
                    is_deleted = table.Column<bool>(type: "boolean", nullable: false, defaultValue: false),
                    updated_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    updated_by = table.Column<Guid>(type: "uuid", nullable: true),
                    xmin = table.Column<uint>(type: "xid", rowVersion: true, nullable: false),
                    contract_allowance = table.Column<decimal>(type: "numeric(18,0)", precision: 18, scale: 0, nullable: true),
                    contract_base_salary = table.Column<decimal>(type: "numeric(18,0)", precision: 18, scale: 0, nullable: true),
                    contract_end = table.Column<DateOnly>(type: "date", nullable: true),
                    contract_no = table.Column<string>(type: "text", nullable: false),
                    contract_start = table.Column<DateOnly>(type: "date", nullable: false),
                    contract_type = table.Column<string>(type: "text", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_employees", x => x.id);
                    table.ForeignKey(
                        name: "fk_employees_employees_manager_id",
                        column: x => x.manager_id,
                        principalSchema: "hr",
                        principalTable: "employees",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "teacher_profiles",
                schema: "hr",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    nationality = table.Column<string>(type: "text", nullable: false),
                    levels = table.Column<string[]>(type: "text[]", nullable: false),
                    certificates = table.Column<List<string>>(type: "text[]", nullable: false),
                    rate_offline = table.Column<decimal>(type: "numeric(18,0)", precision: 18, scale: 0, nullable: false),
                    rate_online = table.Column<decimal>(type: "numeric(18,0)", precision: 18, scale: 0, nullable: false),
                    availability = table.Column<string>(type: "jsonb", nullable: false),
                    extra_branch_ids = table.Column<Guid[]>(type: "uuid[]", nullable: false),
                    bio = table.Column<string>(type: "text", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_teacher_profiles", x => x.id);
                    table.ForeignKey(
                        name: "fk_teacher_profiles_employees_id",
                        column: x => x.id,
                        principalSchema: "hr",
                        principalTable: "employees",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "ix_employees_branch_id",
                schema: "hr",
                table: "employees",
                column: "branch_id");

            migrationBuilder.CreateIndex(
                name: "ix_employees_code",
                schema: "hr",
                table: "employees",
                column: "code",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "ix_employees_email",
                schema: "hr",
                table: "employees",
                column: "email",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "ix_employees_manager_id",
                schema: "hr",
                table: "employees",
                column: "manager_id");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "teacher_profiles",
                schema: "hr");

            migrationBuilder.DropTable(
                name: "employees",
                schema: "hr");
        }
    }
}
