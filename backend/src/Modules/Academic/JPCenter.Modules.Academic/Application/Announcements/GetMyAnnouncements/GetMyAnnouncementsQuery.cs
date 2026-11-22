using JPCenter.Application.Common.Messaging;
using JPCenter.Modules.Academic.Application.Abstractions;
using JPCenter.Modules.Academic.Application.Announcements.Common;
using JPCenter.Modules.Academic.Application.Common;
using JPCenter.Modules.Academic.Application.Learning.Common;
using JPCenter.Modules.Academic.Domain.Announcements;
using JPCenter.Modules.Academic.Domain.Classes;
using JPCenter.Modules.Finance.Contracts;
using JPCenter.SharedKernel.Primitives;
using Microsoft.EntityFrameworkCore;

namespace JPCenter.Modules.Academic.Application.Announcements.GetMyAnnouncements;

/// <summary>
/// ND-07 — cổng HV / PH: thông báo đã gửi của các lớp HV đã ghi danh, gửi tới HV (cổng HV) / PH (cổng PH), cả lớp hoặc có tên HV.
/// Thông báo "chỉ HV nợ phí" hiện khi HV đang nợ quá hạn. <c>Read</c> = tài khoản này đã mở.
/// </summary>
public sealed record GetMyAnnouncementsQuery(Guid? StudentId) : IQuery<IReadOnlyList<PortalAnnouncementDto>>;

public sealed class GetMyAnnouncementsHandler(IAcademicDbContext db, LearningAccess access, PeopleNames people, ITuitionQuery tuition)
    : IQueryHandler<GetMyAnnouncementsQuery, IReadOnlyList<PortalAnnouncementDto>>
{
    public async Task<Result<IReadOnlyList<PortalAnnouncementDto>>> Handle(GetMyAnnouncementsQuery q, CancellationToken ct)
    {
        var learner = await access.LearnerAsync(q.StudentId, ct);
        if (learner.IsFailure) return learner.Error;
        var me = learner.Value.StudentId;
        var classIds = learner.Value.ClassIds.Select(c => new ClassId(c)).ToList();
        var audience = access.IsParent ? AnnouncementAudience.Parent : AnnouncementAudience.Student;
        var debtor = (await tuition.OverdueStudentsAsync([me], ct)).Contains(me);

        var list = (await db.ClassAnnouncements.AsNoTracking().Where(a => classIds.Contains(a.ClassId) && a.SentAt != null).ToListAsync(ct))
            .Where(a => a.Audience.Contains(audience) && (a.StudentIds.Count == 0 || a.StudentIds.Contains(me)) && (!a.DebtorsOnly || debtor)
                        && a.Channels.Contains(AnnouncementChannel.Portal))
            .OrderByDescending(a => a.SentAt).ToList();
        var codes = await db.Classes.AsNoTracking().IgnoreQueryFilters().Where(c => classIds.Contains(c.Id)).ToDictionaryAsync(c => c.Id, c => c.Code, ct);
        var names = await people.OfAsync(list.Select(a => (Guid?)a.SentBy), ct);
        return list.Select(a => new PortalAnnouncementDto(a.Id.Value, a.ClassId.Value, codes.GetValueOrDefault(a.ClassId, "—"), a.Title, a.Content,
            names.GetValueOrDefault(a.SentBy, "Trung tâm"), a.SentAt!.Value, a.ReadBy.Contains(access.AccountId))).ToList();
    }
}
