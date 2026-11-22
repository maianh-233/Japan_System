using JPCenter.Application.Common.IntegrationEvents;
using JPCenter.Application.Common.Security;
using JPCenter.Modules.Crm.Contracts;
using JPCenter.Modules.Notifications.Contracts.IntegrationEvents;
using JPCenter.Modules.System.Contracts;

namespace JPCenter.Modules.Academic.Application.Learning.Common;

/// <summary>Người nhận thật sự của 1 lần gửi: HV, PH (Crm.Lead), GV (Hr.Employee)</summary>
public sealed record LearningReach(IReadOnlyList<Guid> Students, IReadOnlyList<Guid> Guardians, IReadOnlyList<Guid> Teachers)
{
    /// <summary>Số người nhận (HV + PH + GV)</summary>
    public int People => Students.Count + Guardians.Count + Teachers.Count;
}

/// <summary>
/// Thông báo chuông về nội dung học (user story 12 — bài tập mới, điểm, kết quả thi, thông báo lớp) tới cổng HV / PH / GV, phát
/// <see cref="NotificationRequested"/> qua outbox. Gọi TRƯỚC <c>SaveChangesAsync</c>. PH = người liên hệ chính của HV (Crm), trừ khi HV tự là người liên hệ.
/// </summary>
public sealed class LearningNotices(IIntegrationEventPublisher events, ICurrentUser user, IPortalAccountDirectory portals, IGuardianDirectory guardians)
{
    /// <summary>PH (người liên hệ chính, không phải chính HV) của các HV</summary>
    public async Task<IReadOnlyList<Guid>> GuardiansOfAsync(IReadOnlyCollection<Guid> studentIds, CancellationToken ct)
    {
        if (studentIds.Count == 0) return [];
        var deciders = await guardians.DecidersOfAsync(studentIds, ct);
        return deciders.Values.Where(g => g.Relationship != "self").Select(g => g.LeadId).Distinct().ToList();
    }

    /// <summary>Gửi cho HV (link cổng HV), PH (link cổng PH), GV (link cổng GV)</summary>
    public async Task SendAsync(LearningReach to, string title, string? description, string tone, string eventKey, string studentLink,
        string parentLink, string teacherLink, CancellationToken ct)
    {
        var studentAccounts = await portals.ActiveAccountsAsync(to.Students, [], ct);
        if (studentAccounts.Count > 0)
            Publish(new NotificationRequested(title, description, tone, studentLink, eventKey, PortalAccountIds: studentAccounts));
        var parentAccounts = await portals.ActiveAccountsAsync([], to.Guardians, ct);
        if (parentAccounts.Count > 0)
            Publish(new NotificationRequested(title, description, tone, parentLink, eventKey, PortalAccountIds: parentAccounts));
        if (to.Teachers.Count > 0)
            Publish(new NotificationRequested(title, description, tone, teacherLink, eventKey, EmployeeIds: to.Teachers));
    }

    /// <summary>Gửi cho HV + PH của họ</summary>
    public async Task ToLearnersAsync(IReadOnlyCollection<Guid> studentIds, string title, string? description, string tone, string eventKey,
        string studentLink, string parentLink, CancellationToken ct)
    {
        if (studentIds.Count == 0) return;
        await SendAsync(new LearningReach(studentIds.ToList(), await GuardiansOfAsync(studentIds, ct), []), title, description, tone, eventKey,
            studentLink, parentLink, "/teacher/classes", ct);
    }

    private void Publish(NotificationRequested e) => events.Publish(e with { ExcludeAccountId = user.AccountId });
}
