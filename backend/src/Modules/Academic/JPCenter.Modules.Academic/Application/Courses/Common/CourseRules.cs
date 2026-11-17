using JPCenter.Modules.Academic.Application.Abstractions;
using JPCenter.Modules.Academic.Domain.Courses;
using JPCenter.Modules.Academic.Domain.Curricula;
using JPCenter.Modules.System.Contracts;
using JPCenter.SharedKernel.Primitives;
using Microsoft.EntityFrameworkCore;

namespace JPCenter.Modules.Academic.Application.Courses.Common;

/// <summary>Quy tắc cần dữ liệu ngoài aggregate khóa: trùng mã, chi nhánh hợp lệ, trạng thái lộ trình</summary>
public sealed class CourseRules(IAcademicDbContext db, IBranchDirectory branches)
{
    public static readonly Error BranchInactive = Error.Validation("system.branch.inactive", "Chỉ chọn chi nhánh đang hoạt động / đang chuẩn bị");

    /// <summary>Mã khóa chưa dùng (không phân biệt hoa thường — mã luôn lưu chữ hoa). <paramref name="except"/> = khóa đang sửa</summary>
    public async Task<Result> EnsureCodeUniqueAsync(string code, CourseId? except, CancellationToken ct)
    {
        var normalized = Course.NormalizeCode(code);
        var query = db.Courses.Where(c => c.Code == normalized);
        if (except is { } id) query = query.Where(c => c.Id != id);
        var taken = await query.AnyAsync(ct);
        return taken ? CourseErrors.DuplicateCode : Result.Success();
    }

    /// <summary>
    /// Chi nhánh bán phải tồn tại và chưa đóng (KH-02.E7). Khi sửa, chi nhánh đã có từ trước được giữ dù đã đóng (lớp cũ vẫn tra cứu).
    /// </summary>
    public async Task<Result> EnsureBranchesAsync(IEnumerable<Guid> branchIds, IEnumerable<Guid>? existing, CancellationToken ct)
    {
        var all = (await branches.ListAsync(false, ct)).ToDictionary(b => b.Id);
        var keep = existing?.ToHashSet() ?? [];
        foreach (var id in branchIds.Distinct())
        {
            if (!all.TryGetValue(id, out var b)) return BranchInactive;
            if (!b.AcceptsNewData && !keep.Contains(id)) return BranchInactive;
        }
        return Result.Success();
    }

    /// <summary>Lộ trình mặc định của khóa đã phát hành chưa (KH-05.E1)</summary>
    public async Task<bool> DefaultPublishedAsync(Course course, CancellationToken ct) =>
        course.DefaultCurriculumId is { } id
        && await db.Curricula.AnyAsync(c => c.Id == id && c.Status == CurriculumStatus.Published, ct);
}
