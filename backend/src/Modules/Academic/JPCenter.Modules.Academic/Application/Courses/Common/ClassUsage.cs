using JPCenter.Modules.Academic.Application.Abstractions;
using JPCenter.Modules.Academic.Domain.Classes;
using JPCenter.Modules.Academic.Domain.Courses;
using JPCenter.Modules.Academic.Domain.Curricula;
using Microsoft.EntityFrameworkCore;

namespace JPCenter.Modules.Academic.Application.Courses.Common;

/// <summary>
/// Lớp đang dùng khóa / lộ trình — cho các quy tắc KH-04.E1/E2 (khóa mã, hình thức), KH-06.E2 (không gỡ lộ trình đang dạy),
/// LT-11 (xóa / lưu trữ), cột "lớp đang mở". Lớp "đang mở" = chờ khai giảng / đang học / tạm dừng.
/// </summary>
public sealed class ClassUsage(IAcademicDbContext db)
{
    private IQueryable<Class> Open => db.Classes.AsNoTracking()
        .Where(c => c.Status == ClassStatus.Enrolling || c.Status == ClassStatus.Ongoing || c.Status == ClassStatus.Paused);

    /// <summary>Mã lớp đang mở theo khóa</summary>
    public async Task<IReadOnlyDictionary<Guid, IReadOnlyList<string>>> OpenClassesByCourseAsync(CancellationToken ct)
    {
        var rows = await Open.Select(c => new { c.CourseId, c.Code }).ToListAsync(ct);
        return rows.GroupBy(r => r.CourseId.Value)
            .ToDictionary(g => g.Key, g => (IReadOnlyList<string>)g.Select(r => r.Code).Order().ToList());
    }

    /// <summary>Khóa đã từng có lớp (kể cả đã kết thúc / đã hủy)</summary>
    public Task<bool> CourseHasClassesAsync(Guid courseId, CancellationToken ct) =>
        db.Classes.IgnoreQueryFilters().AnyAsync(c => c.CourseId == new CourseId(courseId), ct);

    /// <summary>Mã lớp đang mở học theo lộ trình (lọc theo khóa nếu có)</summary>
    public async Task<IReadOnlyList<string>> OpenClassesUsingAsync(Guid curriculumId, Guid? courseId, CancellationToken ct)
    {
        var query = Open.Where(c => c.CurriculumId == new CurriculumId(curriculumId));
        if (courseId is { } id) query = query.Where(c => c.CourseId == new CourseId(id));
        return (await query.Select(c => c.Code).ToListAsync(ct)).Order().ToList();
    }

    /// <summary>Số lớp (mọi trạng thái) học theo từng lộ trình</summary>
    public async Task<IReadOnlyDictionary<Guid, int>> ClassCountByCurriculumAsync(CancellationToken ct)
    {
        var rows = await db.Classes.AsNoTracking().Where(c => c.CurriculumId != null)
            .GroupBy(c => c.CurriculumId).Select(g => new { g.Key, Count = g.Count() }).ToListAsync(ct);
        return rows.ToDictionary(r => r.Key!.Value.Value, r => r.Count);
    }
}
