using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace JPCenter.Modules.System.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class BranchInvoicePrefixUnique : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateIndex(
                name: "ix_branches_invoice_prefix",
                schema: "system",
                table: "branches",
                column: "invoice_prefix",
                unique: true,
                filter: "is_deleted = false");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "ix_branches_invoice_prefix",
                schema: "system",
                table: "branches");
        }
    }
}
