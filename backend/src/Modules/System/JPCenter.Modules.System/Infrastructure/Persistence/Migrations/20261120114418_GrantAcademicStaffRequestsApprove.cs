using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace JPCenter.Modules.System.Infrastructure.Persistence.Migrations
{
    /// <summary>
    /// Dữ liệu (không đổi cấu trúc): vai trò mẫu Giáo vụ (R-GV) được duyệt đơn hủy buổi / xin nghỉ, mời dạy thay
    /// (<c>academic.requests.approve</c>, phạm vi chi nhánh — DT-16, DT-18). Chỉ chèn khi vai trò tồn tại và chưa có quyền;
    /// DB mới thì seeder nạp sẵn quyền này.
    /// </summary>
    public partial class GrantAcademicStaffRequestsApprove : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.Sql("""
                INSERT INTO system.role_grants (role_id, permission, scope)
                SELECT r.id, 'academic.requests.approve', 'branch' FROM system.roles r
                WHERE r.id = '0bd77e0d-84ab-3373-9875-be22514a1fa3'
                ON CONFLICT DO NOTHING;
                """);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.Sql("""
                DELETE FROM system.role_grants
                WHERE role_id = '0bd77e0d-84ab-3373-9875-be22514a1fa3' AND permission = 'academic.requests.approve';
                """);
        }
    }
}
