using JPCenter.Infrastructure.Common.Persistence;
using JPCenter.Modules.Students.Application.Abstractions;
using JPCenter.Modules.Students.Domain.Enrollments;
using JPCenter.Modules.Students.Domain.Students;
using Microsoft.EntityFrameworkCore;

namespace JPCenter.Modules.Students.Infrastructure.Persistence;

/// <summary>
/// DbContext module Students — schema <c>students</c>: hồ sơ học viên, ghi danh, chứng nhận (user story 06, giai đoạn 14).
/// Chăm sóc / JLPT / du học thêm khi làm các story đó.
/// </summary>
public sealed class StudentsDbContext(DbContextOptions<StudentsDbContext> options) : ModuleDbContextBase(options), IStudentsDbContext
{
    public const string SchemaName = "students";

    protected override string Schema => SchemaName;

    public DbSet<Student> Students => Set<Student>();
    public DbSet<Enrollment> Enrollments => Set<Enrollment>();
}
