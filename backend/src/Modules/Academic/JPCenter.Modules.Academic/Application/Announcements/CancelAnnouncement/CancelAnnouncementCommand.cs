using JPCenter.Application.Common.Messaging;
using JPCenter.Application.Common.Security;
using JPCenter.Modules.Academic.Application.Abstractions;
using JPCenter.Modules.Academic.Application.Announcements.Common;
using JPCenter.Modules.Academic.Application.Learning.Common;
using JPCenter.Modules.Academic.Domain.Announcements;
using JPCenter.SharedKernel.Domain;
using JPCenter.SharedKernel.Primitives;
using Microsoft.EntityFrameworkCore;

namespace JPCenter.Modules.Academic.Application.Announcements.CancelAnnouncement;

/// <summary>ND-07.E3 — chỉ hủy được thông báo đang chờ hẹn giờ; đã gửi → <c>academic.announcement.not_pending</c></summary>
public sealed record CancelAnnouncementCommand(Guid Id, bool AsTeacher) : ICommand<AnnouncementDto>;

public sealed class CancelAnnouncementHandler(IAcademicDbContext db, LearningAccess access, AnnouncementReader reader, IClock clock)
    : ICommandHandler<CancelAnnouncementCommand, AnnouncementDto>
{
    public async Task<Result<AnnouncementDto>> Handle(CancelAnnouncementCommand cmd, CancellationToken ct)
    {
        var a = await db.ClassAnnouncements.FirstOrDefaultAsync(x => x.Id == new ClassAnnouncementId(cmd.Id), ct);
        if (a is null) return AnnouncementErrors.NotFound;
        var cls = await access.ClassAsync(a.ClassId.Value, cmd.AsTeacher, PermissionAction.Update, ct);
        if (cls.IsFailure) return cls.Error.Type == ErrorType.NotFound ? AnnouncementErrors.NotFound : cls.Error;
        var r = a.Cancel(clock.UtcNow);
        if (r.IsFailure) return r.Error;
        await db.SaveChangesAsync(ct);
        return (await reader.ToDtosAsync([a], ct))[0];
    }
}
