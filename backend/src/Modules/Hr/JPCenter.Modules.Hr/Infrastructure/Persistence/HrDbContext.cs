using JPCenter.Modules.Hr.Application.Abstractions;
using JPCenter.Infrastructure.Common.Persistence;
using JPCenter.Modules.Hr.Domain.Employees;
using Microsoft.EntityFrameworkCore;

namespace JPCenter.Modules.Hr.Infrastructure.Persistence;

/// <summary>
/// DbContext module Hr — schema <c>hr</c>: nhân viên + hồ sơ giảng dạy + lịch sử hợp đồng (user story 04).
/// Chấm công, đơn từ, tuyển dụng, onboarding thêm khi làm các story đó.
/// </summary>
public sealed class HrDbContext(DbContextOptions<HrDbContext> options) : ModuleDbContextBase(options), IHrDbContext
{
    public const string SchemaName = "hr";

    protected override string Schema => SchemaName;

    public DbSet<Employee> Employees => Set<Employee>();
}
