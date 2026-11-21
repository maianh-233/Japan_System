using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace JPCenter.Modules.Crm.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    /// <summary>
    /// Không đổi cấu trúc bảng: <c>lead_student_links.student_id</c> (thuộc khóa chính) khai báo không tự sinh — EF thêm liên kết vào lead
    /// có sẵn bằng INSERT (trước đó UPDATE 0 dòng → lỗi chống sửa đè).
    /// </summary>
    public partial class CrmLinkKeyNotGenerated : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {

        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {

        }
    }
}
