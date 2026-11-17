using JPCenter.Application.Common.Auditing;
using JPCenter.Application.Common.Messaging;
using JPCenter.Modules.Academic.Application.Abstractions;
using JPCenter.Modules.Academic.Application.Courses.Common;
using JPCenter.Modules.Academic.Domain.Courses;
using JPCenter.SharedKernel.Primitives;
using Microsoft.EntityFrameworkCore;

namespace JPCenter.Modules.Academic.Application.Courses.PauseCourse;

/// <summary>KH-05 — ngừng tuyển sinh: không mở lớp / ghi danh mới; lớp đang mở học tiếp</summary>
public sealed record PauseCourseCommand(Guid Id) : ICommand<CourseDto>;

public sealed class PauseCourseHandler(IAcademicDbContext db, CourseReader reader, IAuditTrail audit) : ICommandHandler<PauseCourseCommand, CourseDto>
{
    public async Task<Result<CourseDto>> Handle(PauseCourseCommand cmd, CancellationToken ct)
    {
        var course = await db.Courses.FirstOrDefaultAsync(c => c.Id == new CourseId(cmd.Id), ct);
        if (course is null) return CourseErrors.NotFound;
        if (course.Pause() is { IsFailure: true } r) return r.Error;

        await db.SaveChangesAsync(ct);
        audit.Record(AuditCategory.Data, AuditSeverity.Warning, "Ngừng tuyển sinh khóa học", course.Name, course.Code);
        return await reader.ReloadAsync(course.Id, ct);
    }
}
