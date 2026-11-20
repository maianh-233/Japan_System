using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace JPCenter.Modules.System.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class AuditLogBranchIndex : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateIndex(
                name: "ix_audit_logs_branch_id_at",
                schema: "system",
                table: "audit_logs",
                columns: new[] { "branch_id", "at" });
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "ix_audit_logs_branch_id_at",
                schema: "system",
                table: "audit_logs");
        }
    }
}
