using JPCenter.Modules.Hr.Domain.Employees;
using JPCenter.Modules.Hr.Domain.HrRequests;
using JPCenter.Modules.Hr.Domain.Recruitment;
using JPCenter.Modules.Hr.Domain.TimeRecords;
using Microsoft.EntityFrameworkCore;

namespace JPCenter.Modules.Hr.Application.Abstractions;

/// <summary>DbContext module Hr cho tầng Application (Infrastructure hiện thực bằng HrDbContext).</summary>
public interface IHrDbContext
{
    DbSet<Employee> Employees { get; }
    DbSet<TimeRecord> TimeRecords { get; }
    DbSet<HrRequest> HrRequests { get; }
    DbSet<Job> Jobs { get; }
    DbSet<Candidate> Candidates { get; }
    DbSet<Onboarding> Onboardings { get; }

    Task<int> SaveChangesAsync(CancellationToken ct = default);
}
