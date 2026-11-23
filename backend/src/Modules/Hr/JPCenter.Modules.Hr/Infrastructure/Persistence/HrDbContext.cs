using JPCenter.Modules.Hr.Application.Abstractions;
using JPCenter.Infrastructure.Common.Persistence;
using JPCenter.Modules.Hr.Domain.Employees;
using JPCenter.Modules.Hr.Domain.HrRequests;
using JPCenter.Modules.Hr.Domain.Recruitment;
using JPCenter.Modules.Hr.Domain.TimeRecords;
using Microsoft.EntityFrameworkCore;

namespace JPCenter.Modules.Hr.Infrastructure.Persistence;

/// <summary>
/// DbContext module Hr — schema <c>hr</c>: nhân viên + hồ sơ giảng dạy + lịch sử hợp đồng (user story 04);
/// chấm công, đơn từ, tuyển dụng, onboarding (user story 13, giai đoạn 21).
/// </summary>
public sealed class HrDbContext(DbContextOptions<HrDbContext> options) : ModuleDbContextBase(options), IHrDbContext
{
    public const string SchemaName = "hr";

    protected override string Schema => SchemaName;

    public DbSet<Employee> Employees => Set<Employee>();
    public DbSet<TimeRecord> TimeRecords => Set<TimeRecord>();
    public DbSet<HrRequest> HrRequests => Set<HrRequest>();
    public DbSet<Job> Jobs => Set<Job>();
    public DbSet<Candidate> Candidates => Set<Candidate>();
    public DbSet<Onboarding> Onboardings => Set<Onboarding>();
}
