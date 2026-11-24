using JPCenter.Infrastructure.Common.Persistence;
using JPCenter.Modules.Students.Application.Abstractions;
using JPCenter.Modules.Students.Domain.Abroad;
using JPCenter.Modules.Students.Domain.CareCases;
using JPCenter.Modules.Students.Domain.Enrollments;
using JPCenter.Modules.Students.Domain.Jlpt;
using JPCenter.Modules.Students.Domain.Students;
using Microsoft.EntityFrameworkCore;

namespace JPCenter.Modules.Students.Infrastructure.Persistence;

/// <summary>
/// DbContext module Students — schema <c>students</c>: hồ sơ học viên, ghi danh, chứng nhận (user story 06, giai đoạn 14),
/// cảnh báo bỏ học & chăm sóc (HV-14, giai đoạn 15),
/// kỳ thi + đăng ký JLPT (HV-16), hồ sơ du học / XKLĐ (HV-17) — giai đoạn 23.
/// </summary>
public sealed class StudentsDbContext(DbContextOptions<StudentsDbContext> options) : ModuleDbContextBase(options), IStudentsDbContext
{
    public const string SchemaName = "students";

    protected override string Schema => SchemaName;

    public DbSet<Student> Students => Set<Student>();
    public DbSet<Enrollment> Enrollments => Set<Enrollment>();
    public DbSet<CareCase> CareCases => Set<CareCase>();
    public DbSet<CareLog> CareLogs => Set<CareLog>();
    public DbSet<JlptSession> JlptSessions => Set<JlptSession>();
    public DbSet<JlptRegistration> JlptRegistrations => Set<JlptRegistration>();
    public DbSet<AbroadCase> AbroadCases => Set<AbroadCase>();
}
