using JPCenter.Application.Common.Messaging;
using JPCenter.Modules.Academic.Application.Abstractions;
using JPCenter.Modules.Academic.Application.Courses.Common;
using JPCenter.Modules.Academic.Domain.Courses;
using JPCenter.SharedKernel.Primitives;
using Microsoft.EntityFrameworkCore;

namespace JPCenter.Modules.Academic.Application.Courses.GetCourseOptions;

/// <summary>
/// KH-01.E5 — khóa đang tuyển sinh cho ô chọn ở trang khác (Tuyển sinh, Lớp học, Ưu đãi…). Mọi nhân viên đã đăng nhập,
/// không cần quyền trang Khóa học. <paramref name="BranchId"/> = chỉ khóa bán ở chi nhánh đó.
/// </summary>
public sealed record GetCourseOptionsQuery(Guid? BranchId) : IQuery<IReadOnlyList<CourseOptionDto>>;

public sealed record CourseOptionDto(Guid Id, string Code, string Name, CourseCategory Category, string LevelFrom, string LevelTo,
    CourseFormat Format, TeacherType TeacherType, StudyMode Mode, decimal Fee, IReadOnlyList<Guid> BranchIds, Guid? DefaultCurriculumId);

public sealed class GetCourseOptionsHandler(IAcademicDbContext db) : IQueryHandler<GetCourseOptionsQuery, IReadOnlyList<CourseOptionDto>>
{
    public async Task<Result<IReadOnlyList<CourseOptionDto>>> Handle(GetCourseOptionsQuery query, CancellationToken ct)
    {
        var courses = await db.Courses.AsNoTracking().Where(c => c.Status == CourseStatus.Active).OrderBy(c => c.Code).ToListAsync(ct);
        return courses
            .Where(c => query.BranchId is not { } b || c.BranchIds.Any(x => x.Value == b))
            .Select(c => new CourseOptionDto(c.Id.Value, c.Code, c.Name, c.Category, LevelText.Of(c.LevelFrom), LevelText.Of(c.LevelTo), c.Format,
                c.TeacherType, c.Mode, c.Fee.Amount, c.BranchIds.Select(x => x.Value).ToList(), c.DefaultCurriculumId?.Value))
            .ToList();
    }
}
