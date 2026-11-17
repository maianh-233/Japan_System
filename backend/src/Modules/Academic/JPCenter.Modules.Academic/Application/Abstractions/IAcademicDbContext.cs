using JPCenter.Modules.Academic.Domain.Courses;
using JPCenter.Modules.Academic.Domain.Curricula;
using Microsoft.EntityFrameworkCore;

namespace JPCenter.Modules.Academic.Application.Abstractions;

/// <summary>DbContext module Academic cho tầng Application (Infrastructure hiện thực bằng AcademicDbContext).</summary>
public interface IAcademicDbContext
{
    DbSet<Course> Courses { get; }
    DbSet<Curriculum> Curricula { get; }

    Task<int> SaveChangesAsync(CancellationToken ct = default);
}
