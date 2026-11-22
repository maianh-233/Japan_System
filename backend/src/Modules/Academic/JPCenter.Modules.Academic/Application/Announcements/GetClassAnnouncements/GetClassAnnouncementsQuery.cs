using JPCenter.Application.Common.Messaging;
using JPCenter.Application.Common.Security;
using JPCenter.Modules.Academic.Application.Abstractions;
using JPCenter.Modules.Academic.Application.Announcements.Common;
using JPCenter.Modules.Academic.Application.Learning.Common;
using JPCenter.SharedKernel.Primitives;
using Microsoft.EntityFrameworkCore;

namespace JPCenter.Modules.Academic.Application.Announcements.GetClassAnnouncements;

/// <summary>ND-07 — tab Thông báo của lớp (nhân viên <c>academic.classes.view</c> / GV của lớp), mới nhất trước</summary>
public sealed record GetClassAnnouncementsQuery(Guid ClassId, bool AsTeacher) : IQuery<IReadOnlyList<AnnouncementDto>>;

public sealed class GetClassAnnouncementsHandler(IAcademicDbContext db, LearningAccess access, AnnouncementReader reader)
    : IQueryHandler<GetClassAnnouncementsQuery, IReadOnlyList<AnnouncementDto>>
{
    public async Task<Result<IReadOnlyList<AnnouncementDto>>> Handle(GetClassAnnouncementsQuery q, CancellationToken ct)
    {
        var cls = await access.ClassAsync(q.ClassId, q.AsTeacher, PermissionAction.View, ct);
        if (cls.IsFailure) return cls.Error;
        var list = (await db.ClassAnnouncements.AsNoTracking().Where(a => a.ClassId == cls.Value.Id).ToListAsync(ct))
            .OrderByDescending(a => a.SentAt ?? a.ScheduledAt).ToList();
        return Result.Success(await reader.ToDtosAsync(list, ct));
    }
}
