using JPCenter.Application.Common.Messaging;
using JPCenter.Modules.Academic.Application.Courses.Common;
using JPCenter.Modules.Academic.Domain.Courses;
using JPCenter.SharedKernel.Primitives;

namespace JPCenter.Modules.Academic.Application.Courses.GetCourse;

/// <summary>KH-01 — chi tiết 1 khóa (ngoài phạm vi xem → 404)</summary>
public sealed record GetCourseQuery(Guid Id) : IQuery<CourseDto>;

public sealed class GetCourseHandler(CourseReader reader) : IQueryHandler<GetCourseQuery, CourseDto>
{
    public async Task<Result<CourseDto>> Handle(GetCourseQuery query, CancellationToken ct) =>
        await reader.FindAsync(query.Id, ct) is { } dto ? dto : CourseErrors.NotFound;
}
