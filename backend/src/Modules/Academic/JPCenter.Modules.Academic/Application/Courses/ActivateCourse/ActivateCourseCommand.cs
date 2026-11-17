using JPCenter.Application.Common.Auditing;
using JPCenter.Application.Common.Messaging;
using JPCenter.Modules.Academic.Application.Abstractions;
using JPCenter.Modules.Academic.Application.Courses.Common;
using JPCenter.Modules.Academic.Domain.Courses;
using JPCenter.SharedKernel.Primitives;
using Microsoft.EntityFrameworkCore;

namespace JPCenter.Modules.Academic.Application.Courses.ActivateCourse;

/// <summary>KH-05 — mở tuyển sinh. Cần lộ trình mặc định đã phát hành (E1)</summary>
public sealed record ActivateCourseCommand(Guid Id) : ICommand<CourseDto>;

public sealed class ActivateCourseHandler(IAcademicDbContext db, CourseRules rules, CourseReader reader, IAuditTrail audit)
    : ICommandHandler<ActivateCourseCommand, CourseDto>
{
    public async Task<Result<CourseDto>> Handle(ActivateCourseCommand cmd, CancellationToken ct)
    {
        var course = await db.Courses.FirstOrDefaultAsync(c => c.Id == new CourseId(cmd.Id), ct);
        if (course is null) return CourseErrors.NotFound;
        if (course.Activate(await rules.DefaultPublishedAsync(course, ct)) is { IsFailure: true } r) return r.Error;

        await db.SaveChangesAsync(ct);
        audit.Record(AuditCategory.Data, AuditSeverity.Info, "Mở tuyển sinh khóa học", course.Name, course.Code);
        return await reader.ReloadAsync(course.Id, ct);
    }
}
