using JPCenter.Application.Common.Messaging;
using JPCenter.Modules.Academic.Application.Courses.Common;
using JPCenter.SharedKernel.Primitives;

namespace JPCenter.Modules.Academic.Application.Courses.GetCourses;

/// <summary>KH-01 — mọi khóa trong phạm vi xem (lọc / tìm ở frontend: danh mục vài chục khóa)</summary>
public sealed record GetCoursesQuery : IQuery<IReadOnlyList<CourseDto>>;

public sealed class GetCoursesHandler(CourseReader reader) : IQueryHandler<GetCoursesQuery, IReadOnlyList<CourseDto>>
{
    public async Task<Result<IReadOnlyList<CourseDto>>> Handle(GetCoursesQuery query, CancellationToken ct) =>
        Result.Success(await reader.ListAsync(ct));
}
