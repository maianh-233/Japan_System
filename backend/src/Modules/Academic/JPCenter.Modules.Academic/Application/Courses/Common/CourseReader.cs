using JPCenter.Application.Common.Security;
using JPCenter.Modules.Academic.Application.Abstractions;
using JPCenter.Modules.Academic.Domain.Courses;
using JPCenter.SharedKernel.Primitives;
using Microsoft.EntityFrameworkCore;

namespace JPCenter.Modules.Academic.Application.Courses.Common;

/// <summary>
/// Đọc khóa học ra DTO + lọc theo phạm vi quyền xem (KH-01): <c>all</c> mọi khóa · <c>branch</c> / <c>own</c> khóa bán ở chi nhánh được gán.
/// Khóa là danh mục dùng chung — sửa / thêm không giới hạn theo chi nhánh.
/// </summary>
public sealed class CourseReader(IAcademicDbContext db, ICurrentUser user, ClassUsage classes)
{
    public const string Page = "academic.courses";

    public async Task<IReadOnlyList<CourseDto>> ListAsync(CancellationToken ct)
    {
        var rows = await Query(db.Courses).ToListAsync(ct);
        var open = await classes.OpenClassesByCourseAsync(ct);
        var allowed = AllowedBranches();
        return rows
            .Where(r => allowed is null || r.Course.BranchIds.Any(b => allowed.Contains(b.Value)))
            .Select(r => ToDto(r.Course, r.Version, r.CreatedAt, r.UpdatedAt, open))
            .OrderBy(c => c.Code)
            .ToList();
    }

    public async Task<CourseDto?> FindAsync(Guid id, CancellationToken ct)
    {
        var r = await Query(db.Courses.Where(c => c.Id == new CourseId(id))).FirstOrDefaultAsync(ct);
        if (r is null) return null;
        var allowed = AllowedBranches();
        if (allowed is not null && !r.Course.BranchIds.Any(b => allowed.Contains(b.Value))) return null;
        return ToDto(r.Course, r.Version, r.CreatedAt, r.UpdatedAt, await classes.OpenClassesByCourseAsync(ct));
    }

    /// <summary>Đọc lại sau khi ghi — không lọc phạm vi xem (người sửa đã qua kiểm quyền ở endpoint)</summary>
    public async Task<CourseDto> ReloadAsync(CourseId id, CancellationToken ct)
    {
        var r = await Query(db.Courses.Where(c => c.Id == id)).FirstAsync(ct);
        return ToDto(r.Course, r.Version, r.CreatedAt, r.UpdatedAt, await classes.OpenClassesByCourseAsync(ct));
    }

    private static IQueryable<Row> Query(IQueryable<Course> source) => source.AsNoTracking().Select(c => new Row(
        c, EF.Property<uint>(c, "xmin"), EF.Property<DateTimeOffset>(c, "CreatedAt"), EF.Property<DateTimeOffset?>(c, "UpdatedAt")));

    /// <summary>null = xem mọi khóa</summary>
    private HashSet<Guid>? AllowedBranches()
    {
        var perm = user.Permission(Page, PermissionAction.View);
        if (perm is null || perm.Scope == PermissionScope.All) return null;
        if (perm.BranchIds.Count > 0) return perm.BranchIds.ToHashSet();
        var me = user.Access;
        return me is null || me.AllBranches ? null : me.BranchIds.ToHashSet();
    }

    public static CourseDto ToDto(Course c, uint version, DateTimeOffset createdAt, DateTimeOffset? updatedAt,
        IReadOnlyDictionary<Guid, IReadOnlyList<string>> openClasses) => new(
        c.Id.Value, c.Code, c.Name, c.Category, LevelText.Of(c.LevelFrom), LevelText.Of(c.LevelTo), c.Format, c.TeacherType, c.Mode,
        c.Sessions, c.MinutesPerSession, c.SessionsPerWeek, c.Fee.Amount, c.MaxStudents, c.BranchIds.Select(b => b.Value).ToList(),
        c.Description, c.Status,
        c.Curricula.OrderByDescending(x => x.IsDefault).ThenBy(x => x.LinkedAt)
            .Select(x => new CourseCurriculumDto(x.CurriculumId.Value, x.IsDefault, x.LinkedAt)).ToList(),
        openClasses.TryGetValue(c.Id.Value, out var open) ? open : [],
        createdAt, updatedAt ?? createdAt, version);

    private sealed record Row(Course Course, uint Version, DateTimeOffset CreatedAt, DateTimeOffset? UpdatedAt);
}
