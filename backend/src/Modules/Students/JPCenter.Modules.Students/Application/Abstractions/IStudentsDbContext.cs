using JPCenter.Modules.Students.Domain.Abroad;
using JPCenter.Modules.Students.Domain.CareCases;
using JPCenter.Modules.Students.Domain.Enrollments;
using JPCenter.Modules.Students.Domain.Jlpt;
using JPCenter.Modules.Students.Domain.Students;
using Microsoft.EntityFrameworkCore;

namespace JPCenter.Modules.Students.Application.Abstractions;

/// <summary>DbContext module Students cho tầng Application (Infrastructure hiện thực bằng StudentsDbContext).</summary>
public interface IStudentsDbContext
{
    DbSet<Student> Students { get; }
    DbSet<Enrollment> Enrollments { get; }
    DbSet<CareCase> CareCases { get; }
    DbSet<CareLog> CareLogs { get; }
    DbSet<JlptSession> JlptSessions { get; }
    DbSet<JlptRegistration> JlptRegistrations { get; }
    DbSet<AbroadCase> AbroadCases { get; }

    Task<int> SaveChangesAsync(CancellationToken ct = default);
}
