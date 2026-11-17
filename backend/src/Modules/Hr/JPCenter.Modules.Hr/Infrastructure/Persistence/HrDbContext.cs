using JPCenter.Infrastructure.Common.Persistence;
using JPCenter.Modules.Hr.Domain.Employees;
using Microsoft.EntityFrameworkCore;

namespace JPCenter.Modules.Hr.Infrastructure.Persistence;

/// <summary>
/// DbContext module Hr — schema <c>hr</c>. Giai đoạn này chỉ có nhân viên + hồ sơ giảng dạy
/// (đủ cho đăng nhập / phân quyền); các bảng Hr khác thêm khi làm chức năng Nhân sự.
/// </summary>
public sealed class HrDbContext(DbContextOptions<HrDbContext> options) : ModuleDbContextBase(options)
{
    public const string SchemaName = "hr";

    protected override string Schema => SchemaName;

    public DbSet<Employee> Employees => Set<Employee>();
}
