using JPCenter.Application.Common.Auditing;
using JPCenter.Application.Common.Messaging;
using JPCenter.Modules.Academic.Application.Abstractions;
using JPCenter.Modules.Academic.Application.Courses.Common;
using JPCenter.Modules.Academic.Domain.Courses;
using JPCenter.Modules.Academic.Domain.Curricula;
using JPCenter.SharedKernel.Primitives;
using Microsoft.EntityFrameworkCore;

namespace JPCenter.Modules.Academic.Application.Courses.UnlinkCurriculum;

/// <summary>KH-06 — gỡ lộ trình khỏi khóa (chỉ gỡ liên kết). Không gỡ bản mặc định (E1) hoặc đang có lớp mở học theo (E2)</summary>
public sealed record UnlinkCurriculumCommand(Guid CourseId, Guid CurriculumId) : ICommand<CourseDto>;

public sealed class UnlinkCurriculumHandler(IAcademicDbContext db, ClassUsage classes, CourseReader reader, IAuditTrail audit)
    : ICommandHandler<UnlinkCurriculumCommand, CourseDto>
{
    public async Task<Result<CourseDto>> Handle(UnlinkCurriculumCommand cmd, CancellationToken ct)
    {
        var course = await db.Courses.FirstOrDefaultAsync(c => c.Id == new CourseId(cmd.CourseId), ct);
        if (course is null) return CourseErrors.NotFound;
        var open = await classes.OpenClassesUsingAsync(cmd.CurriculumId, cmd.CourseId, ct);
        if (open.Count > 0) return CourseErrors.CurriculumInUse(open);
        if (course.UnlinkCurriculum(new CurriculumId(cmd.CurriculumId)) is { IsFailure: true } r) return r.Error;

        await db.SaveChangesAsync(ct);
        audit.Record(AuditCategory.Data, AuditSeverity.Info, "Gỡ lộ trình khỏi khóa", course.Name, course.Code);
        return await reader.ReloadAsync(course.Id, ct);
    }
}
