using JPCenter.Infrastructure.Common.Persistence;
using JPCenter.Modules.Academic.Application.Abstractions;
using JPCenter.Modules.Academic.Domain.Courses;
using JPCenter.Modules.Academic.Domain.Curricula;
using Microsoft.EntityFrameworkCore;

namespace JPCenter.Modules.Academic.Infrastructure.Persistence;

/// <summary>
/// DbContext module Academic — schema <c>academic</c>: khóa học + lộ trình (user story 09).
/// Lớp, buổi học, phòng, ngân hàng câu hỏi... thêm khi làm các story đó.
/// </summary>
public sealed class AcademicDbContext(DbContextOptions<AcademicDbContext> options) : ModuleDbContextBase(options), IAcademicDbContext
{
    public const string SchemaName = "academic";

    protected override string Schema => SchemaName;

    public DbSet<Course> Courses => Set<Course>();
    public DbSet<Curriculum> Curricula => Set<Curriculum>();
}
