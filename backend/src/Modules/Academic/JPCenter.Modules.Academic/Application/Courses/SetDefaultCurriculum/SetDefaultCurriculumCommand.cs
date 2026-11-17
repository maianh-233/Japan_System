using JPCenter.Application.Common.Auditing;
using JPCenter.Application.Common.Messaging;
using JPCenter.Modules.Academic.Application.Abstractions;
using JPCenter.Modules.Academic.Application.Courses.Common;
using JPCenter.Modules.Academic.Domain.Courses;
using JPCenter.Modules.Academic.Domain.Curricula;
using JPCenter.SharedKernel.Primitives;
using Microsoft.EntityFrameworkCore;

namespace JPCenter.Modules.Academic.Application.Courses.SetDefaultCurriculum;

/// <summary>KH-06 — đổi lộ trình mặc định cho lớp mở mới (lớp đã mở giữ lộ trình đã chốt). Phải là bản đã phát hành (E4)</summary>
public sealed record SetDefaultCurriculumCommand(Guid CourseId, Guid CurriculumId) : ICommand<CourseDto>;

public sealed class SetDefaultCurriculumHandler(IAcademicDbContext db, CourseReader reader, IAuditTrail audit)
    : ICommandHandler<SetDefaultCurriculumCommand, CourseDto>
{
    public async Task<Result<CourseDto>> Handle(SetDefaultCurriculumCommand cmd, CancellationToken ct)
    {
        var course = await db.Courses.FirstOrDefaultAsync(c => c.Id == new CourseId(cmd.CourseId), ct);
        if (course is null) return CourseErrors.NotFound;
        var curriculum = await db.Curricula.AsNoTracking().FirstOrDefaultAsync(c => c.Id == new CurriculumId(cmd.CurriculumId), ct);
        if (curriculum is null) return CurriculumErrors.NotFound;
        if (curriculum.Status != CurriculumStatus.Published) return CourseErrors.DefaultNotPublished;
        if (course.SetDefaultCurriculum(curriculum.Id) is { IsFailure: true } r) return r.Error;

        await db.SaveChangesAsync(ct);
        audit.Record(AuditCategory.Data, AuditSeverity.Info, "Đổi lộ trình mặc định", course.Name,
            $"{course.Code} → {curriculum.Code} {curriculum.Version}");
        return await reader.ReloadAsync(course.Id, ct);
    }
}
