using JPCenter.Application.Common.Auditing;
using JPCenter.Application.Common.Messaging;
using JPCenter.Modules.Academic.Application.Abstractions;
using JPCenter.Modules.Academic.Application.Curricula.Common;
using JPCenter.Modules.Academic.Domain.Courses;
using JPCenter.Modules.Academic.Domain.Curricula;
using JPCenter.SharedKernel.Primitives;
using Microsoft.EntityFrameworkCore;

namespace JPCenter.Modules.Academic.Application.Curricula.PublishCurriculum;

/// <summary>
/// LT-09 — phát hành: khóa nội dung. <paramref name="SetDefaultFor"/> (E5): đặt bản này làm mặc định cho các khóa đã gắn nó
/// (thường khi phát hành phiên bản mới của bản đang mặc định — lớp cũ giữ bản cũ).
/// </summary>
public sealed record PublishCurriculumCommand(Guid Id, List<Guid>? SetDefaultFor) : ICommand<CurriculumDto>;

public sealed class PublishCurriculumHandler(IAcademicDbContext db, CurriculumReader reader, IAuditTrail audit)
    : ICommandHandler<PublishCurriculumCommand, CurriculumDto>
{
    public async Task<Result<CurriculumDto>> Handle(PublishCurriculumCommand cmd, CancellationToken ct)
    {
        var c = await db.Curricula.AsSplitQuery().FirstOrDefaultAsync(x => x.Id == new CurriculumId(cmd.Id), ct);
        if (c is null) return CurriculumErrors.NotFound;
        if (c.Publish() is { IsFailure: true } r) return r.Error;

        if (cmd.SetDefaultFor is { Count: > 0 } courseIds)
        {
            var ids = courseIds.Select(x => new CourseId(x)).ToList();
            var courses = await db.Courses.Where(x => ids.Contains(x.Id)).ToListAsync(ct);
            foreach (var course in courses)
                if (course.SetDefaultCurriculum(c.Id) is { IsFailure: true } notLinked) return notLinked.Error;
        }

        await db.SaveChangesAsync(ct);
        var materials = c.AllMaterials.Count();
        audit.Record(AuditCategory.Data, AuditSeverity.Info, "Phát hành lộ trình", c.Name,
            $"{c.Code} {c.Version} · {c.Lessons.Count} bài · {materials} tài liệu");
        return (await reader.FindAsync(c.Id, ct))!;
    }
}
