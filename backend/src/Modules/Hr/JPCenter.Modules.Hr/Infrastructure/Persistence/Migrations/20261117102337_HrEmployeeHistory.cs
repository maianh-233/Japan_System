using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace JPCenter.Modules.Hr.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class HrEmployeeHistory : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "quit_reason",
                schema: "hr",
                table: "employees",
                type: "character varying(500)",
                maxLength: 500,
                nullable: true);

            migrationBuilder.CreateTable(
                name: "employment_contracts",
                schema: "hr",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    type = table.Column<string>(type: "text", nullable: false),
                    no = table.Column<string>(type: "character varying(50)", maxLength: 50, nullable: false),
                    start = table.Column<DateOnly>(type: "date", nullable: false),
                    end = table.Column<DateOnly>(type: "date", nullable: true),
                    base_salary = table.Column<decimal>(type: "numeric(18,0)", precision: 18, scale: 0, nullable: true),
                    allowance = table.Column<decimal>(type: "numeric(18,0)", precision: 18, scale: 0, nullable: true),
                    replaced_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    replaced_by = table.Column<Guid>(type: "uuid", nullable: true),
                    employee_id = table.Column<Guid>(type: "uuid", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_employment_contracts", x => x.id);
                    table.ForeignKey(
                        name: "fk_employment_contracts_employees_employee_id",
                        column: x => x.employee_id,
                        principalSchema: "hr",
                        principalTable: "employees",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "ix_employment_contracts_employee_id",
                schema: "hr",
                table: "employment_contracts",
                column: "employee_id");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "employment_contracts",
                schema: "hr");

            migrationBuilder.DropColumn(
                name: "quit_reason",
                schema: "hr",
                table: "employees");
        }
    }
}
