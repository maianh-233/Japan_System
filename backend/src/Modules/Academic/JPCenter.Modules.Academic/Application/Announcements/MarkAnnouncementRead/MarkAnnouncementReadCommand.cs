using JPCenter.Application.Common.Messaging;
using JPCenter.Modules.Academic.Application.Abstractions;
using JPCenter.Modules.Academic.Application.Learning.Common;
using JPCenter.Modules.Academic.Domain.Announcements;
using JPCenter.SharedKernel.Primitives;
using Microsoft.EntityFrameworkCore;

namespace JPCenter.Modules.Academic.Application.Announcements.MarkAnnouncementRead;

/// <summary>ND-07.E4 — HV / PH mở thông báo trên cổng → +1 đã xem (mỗi tài khoản 1 lần). Thông báo không thuộc lớp của tài khoản → 404.</summary>
public sealed record MarkAnnouncementReadCommand(Guid Id) : ICommand;

public sealed class MarkAnnouncementReadHandler(IAcademicDbContext db, LearningAccess access) : ICommandHandler<MarkAnnouncementReadCommand>
{
    public async Task<Result> Handle(MarkAnnouncementReadCommand cmd, CancellationToken ct)
    {
        var a = await db.ClassAnnouncements.FirstOrDefaultAsync(x => x.Id == new ClassAnnouncementId(cmd.Id), ct);
        if (a is null || a.SentAt is null || !(await access.AccountClassIdsAsync(ct)).Contains(a.ClassId.Value)) return AnnouncementErrors.NotFound;
        if (a.MarkRead(access.AccountId)) await db.SaveChangesAsync(ct);
        return Result.Success();
    }
}
