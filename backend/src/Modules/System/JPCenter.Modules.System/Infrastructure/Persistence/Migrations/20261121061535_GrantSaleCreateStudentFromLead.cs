using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace JPCenter.Modules.System.Infrastructure.Persistence.Migrations
{
    /// <summary>
    /// Dữ liệu (không đổi cấu trúc): vai trò mẫu Sale (R-SALE) được tạo hồ sơ học viên khi chốt lead (<c>students.profiles.create</c>,
    /// phạm vi own — CRM-07). Chỉ chèn khi vai trò tồn tại và chưa có quyền; DB mới thì seeder nạp sẵn quyền này.
    /// </summary>
    public partial class GrantSaleCreateStudentFromLead : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.Sql("""
                INSERT INTO system.role_grants (role_id, permission, scope)
                SELECT r.id, 'students.profiles.create', 'own' FROM system.roles r
                WHERE r.id = '1392b320-5710-3efb-888c-6e6c29861576'
                ON CONFLICT DO NOTHING;
                """);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.Sql("""
                DELETE FROM system.role_grants
                WHERE role_id = '1392b320-5710-3efb-888c-6e6c29861576' AND permission = 'students.profiles.create';
                """);
        }
    }
}
