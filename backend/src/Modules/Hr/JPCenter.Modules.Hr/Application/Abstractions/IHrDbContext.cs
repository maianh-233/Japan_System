using JPCenter.Modules.Hr.Domain.Employees;
using Microsoft.EntityFrameworkCore;

namespace JPCenter.Modules.Hr.Application.Abstractions;

/// <summary>DbContext module Hr cho tầng Application (Infrastructure hiện thực bằng HrDbContext).</summary>
public interface IHrDbContext
{
    DbSet<Employee> Employees { get; }

    Task<int> SaveChangesAsync(CancellationToken ct = default);
}
