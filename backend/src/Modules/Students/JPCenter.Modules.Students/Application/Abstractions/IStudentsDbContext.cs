using JPCenter.Modules.Students.Domain.Enrollments;
using JPCenter.Modules.Students.Domain.Students;
using Microsoft.EntityFrameworkCore;

namespace JPCenter.Modules.Students.Application.Abstractions;

/// <summary>DbContext module Students cho tầng Application (Infrastructure hiện thực bằng StudentsDbContext).</summary>
public interface IStudentsDbContext
{
    DbSet<Student> Students { get; }
    DbSet<Enrollment> Enrollments { get; }

    Task<int> SaveChangesAsync(CancellationToken ct = default);
}
