using JPCenter.Modules.Academic.Application.Abstractions;
using JPCenter.Modules.Academic.Domain.Curricula;
using JPCenter.SharedKernel.Primitives;
using Microsoft.EntityFrameworkCore;

namespace JPCenter.Modules.Academic.Application.Curricula.Common;

/// <summary>Quy tắc lộ trình cần dữ liệu ngoài aggregate: trùng mã + phiên bản, đang là mặc định của khóa nào</summary>
public sealed class CurriculumRules(IAcademicDbContext db)
{
    /// <summary>(mã, phiên bản) chưa dùng — LT-02.E2. <paramref name="except"/> = lộ trình đang sửa</summary>
    public async Task<Result> EnsureVersionUniqueAsync(string code, string version, CurriculumId? except, CancellationToken ct)
    {
        var normalized = Curriculum.NormalizeCode(code);
        var v = version.Trim();
        var query = db.Curricula.Where(c => c.Code == normalized && c.Version == v);
        if (except is { } id) query = query.Where(c => c.Id != id);
        return await query.AnyAsync(ct) ? CurriculumErrors.DuplicateVersion : Result.Success();
    }

    /// <summary>Số khóa đang đặt lộ trình này làm mặc định — LT-11.E1</summary>
    public async Task<int> DefaultCourseCountAsync(CurriculumId id, CancellationToken ct)
    {
        var courses = await db.Courses.AsNoTracking().ToListAsync(ct);
        return courses.Count(c => c.Curricula.Any(l => l.CurriculumId == id && l.IsDefault));
    }

    /// <summary>Đã có bản nháp phiên bản mới tạo từ bản này chưa — LT-10.E1</summary>
    public Task<bool> HasDraftSuccessorAsync(CurriculumId id, CancellationToken ct) =>
        db.Curricula.AnyAsync(c => c.PreviousVersionId == id && c.Status == CurriculumStatus.Draft, ct);
}
