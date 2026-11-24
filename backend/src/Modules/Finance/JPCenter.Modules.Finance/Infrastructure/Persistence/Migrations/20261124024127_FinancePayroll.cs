using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace JPCenter.Modules.Finance.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class FinancePayroll : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<Guid>(
                name: "sale_assigned_by",
                schema: "finance",
                table: "invoices",
                type: "uuid",
                nullable: true);

            migrationBuilder.AddColumn<Guid>(
                name: "sale_id",
                schema: "finance",
                table: "invoices",
                type: "uuid",
                nullable: true);

            migrationBuilder.CreateTable(
                name: "commission_tiers",
                schema: "finance",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    from = table.Column<decimal>(type: "numeric(18,0)", precision: 18, scale: 0, nullable: false),
                    percent = table.Column<decimal>(type: "numeric(5,2)", precision: 5, scale: 2, nullable: false),
                    created_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    created_by = table.Column<Guid>(type: "uuid", nullable: true),
                    updated_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    updated_by = table.Column<Guid>(type: "uuid", nullable: true),
                    xmin = table.Column<uint>(type: "xid", rowVersion: true, nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_commission_tiers", x => x.id);
                });

            migrationBuilder.CreateTable(
                name: "pay_adjustments",
                schema: "finance",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    month = table.Column<string>(type: "character varying(7)", maxLength: 7, nullable: false),
                    employee_id = table.Column<Guid>(type: "uuid", nullable: false),
                    amount = table.Column<decimal>(type: "numeric(18,0)", precision: 18, scale: 0, nullable: false),
                    reason = table.Column<string>(type: "character varying(500)", maxLength: 500, nullable: false),
                    by = table.Column<Guid>(type: "uuid", nullable: false),
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
                    table.PrimaryKey("pk_pay_adjustments", x => x.id);
                });

            migrationBuilder.CreateTable(
                name: "payroll_locks",
                schema: "finance",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    kind = table.Column<string>(type: "text", nullable: false),
                    month = table.Column<string>(type: "character varying(7)", maxLength: 7, nullable: false),
                    branch_id = table.Column<Guid>(type: "uuid", nullable: false),
                    locked_by = table.Column<Guid>(type: "uuid", nullable: false),
                    locked_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    total = table.Column<decimal>(type: "numeric(18,0)", precision: 18, scale: 0, nullable: false),
                    lines = table.Column<string>(type: "jsonb", nullable: false),
                    paid_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    unlocked_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    unlocked_by = table.Column<Guid>(type: "uuid", nullable: true),
                    xmin = table.Column<uint>(type: "xid", rowVersion: true, nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_payroll_locks", x => x.id);
                });

            migrationBuilder.CreateTable(
                name: "sale_targets",
                schema: "finance",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    employee_id = table.Column<Guid>(type: "uuid", nullable: false),
                    month = table.Column<string>(type: "character varying(7)", maxLength: 7, nullable: false),
                    target = table.Column<decimal>(type: "numeric(18,0)", precision: 18, scale: 0, nullable: false),
                    created_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    created_by = table.Column<Guid>(type: "uuid", nullable: true),
                    updated_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    updated_by = table.Column<Guid>(type: "uuid", nullable: true),
                    xmin = table.Column<uint>(type: "xid", rowVersion: true, nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_sale_targets", x => x.id);
                });

            migrationBuilder.CreateIndex(
                name: "ix_invoices_sale_id",
                schema: "finance",
                table: "invoices",
                column: "sale_id");

            migrationBuilder.CreateIndex(
                name: "ix_pay_adjustments_month_employee_id",
                schema: "finance",
                table: "pay_adjustments",
                columns: new[] { "month", "employee_id" });

            migrationBuilder.CreateIndex(
                name: "ix_payroll_locks_kind_month_branch_id",
                schema: "finance",
                table: "payroll_locks",
                columns: new[] { "kind", "month", "branch_id" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "ix_sale_targets_employee_id_month",
                schema: "finance",
                table: "sale_targets",
                columns: new[] { "employee_id", "month" },
                unique: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "commission_tiers",
                schema: "finance");

            migrationBuilder.DropTable(
                name: "pay_adjustments",
                schema: "finance");

            migrationBuilder.DropTable(
                name: "payroll_locks",
                schema: "finance");

            migrationBuilder.DropTable(
                name: "sale_targets",
                schema: "finance");

            migrationBuilder.DropIndex(
                name: "ix_invoices_sale_id",
                schema: "finance",
                table: "invoices");

            migrationBuilder.DropColumn(
                name: "sale_assigned_by",
                schema: "finance",
                table: "invoices");

            migrationBuilder.DropColumn(
                name: "sale_id",
                schema: "finance",
                table: "invoices");
        }
    }
}
