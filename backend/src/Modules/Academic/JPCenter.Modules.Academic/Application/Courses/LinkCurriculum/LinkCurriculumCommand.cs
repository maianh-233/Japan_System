using JPCenter.Application.Common.Auditing;
using JPCenter.Application.Common.Messaging;
using JPCenter.Modules.Academic.Application.Abstractions;
using JPCenter.Modules.Academic.Application.Courses.Common;
using JPCenter.Modules.Academic.Domain.Courses;
using JPCenter.Modules.Academic.Domain.Curricula;
using JPCenter.SharedKernel.Domain;
using JPCenter.SharedKernel.Primitives;
using Microsoft.EntityFrameworkCore;

namespace JPCenter.Modules.Academic.Application.Courses.LinkCurriculum;

/// <summary>
/// KH-06 — gắn lộ trình vào khóa. Khóa chưa có lộ trình → tự thành mặc định. Không gắn bản lưu trữ (E5);
/// yêu cầu đặt mặc định thì lộ trình phải đã phát hành (E4). Gắn lại lộ trình đã gắn → bỏ qua (E3).
/// </summary>
public sealed record LinkCurriculumCommand(Guid CourseId, Guid CurriculumId, bool IsDefault) : ICommand<CourseDto>;

public sealed class LinkCurriculumHandler(IAcademicDbContext db, CourseReader reader, IAuditTrail audit, IClock clock)
    : ICommandHandler<LinkCurriculumCommand, CourseDto>
{
    public async Task<Result<CourseDto>> Handle(LinkCurriculumCommand cmd, CancellationToken ct)
    {
        var course = await db.Courses.FirstOrDefaultAsync(c => c.Id == new CourseId(cmd.CourseId), ct);
        if (course is null) return CourseErrors.NotFound;
        var curriculum = await db.Curricula.AsNoTracking().FirstOrDefaultAsync(c => c.Id == new CurriculumId(cmd.CurriculumId), ct);
        if (curriculum is null) return CurriculumErrors.NotFound;
        if (curriculum.Status == CurriculumStatus.Archived) return CurriculumErrors.Archived;
        if (cmd.IsDefault && curriculum.Status != CurriculumStatus.Published) return CourseErrors.DefaultNotPublished;

        course.LinkCurriculum(curriculum.Id, cmd.IsDefault, clock.UtcNow);
        await db.SaveChangesAsync(ct);
        audit.Record(AuditCategory.Data, AuditSeverity.Info, "Gắn lộ trình vào khóa", course.Name,
            $"{course.Code} + {curriculum.Code} {curriculum.Version}{(course.DefaultCurriculumId == curriculum.Id ? " (mặc định)" : "")}");
        return await reader.ReloadAsync(course.Id, ct);
    }
}
