using JPCenter.Application.Common.Messaging;
using JPCenter.Application.Common.Security;
using JPCenter.Modules.Crm.Application.Abstractions;
using JPCenter.Modules.Crm.Application.Common;
using JPCenter.Modules.Crm.Domain.Appointments;
using JPCenter.Modules.Crm.Domain.Campaigns;
using JPCenter.Modules.Crm.Domain.Leads;
using JPCenter.SharedKernel.Domain;
using JPCenter.SharedKernel.Primitives;
using Microsoft.EntityFrameworkCore;

namespace JPCenter.Modules.Crm.Application.Funnel.GetFunnel;

/// <summary>Bước phễu: lead → đã gọi → test → chốt</summary>
public enum FunnelStage
{
    Lead,
    Called,
    Test,
    Won,
}

/// <summary>
/// Hành trình 1 lead qua phễu — giống <c>features/crm/funnel.ts › journeyOf</c>; frontend gom theo Sale / nguồn / chiến dịch.
/// Mốc trống = bỏ qua bước (vẫn tính đã qua, không vào trung bình). <paramref name="StuckDays"/> = đang kẹt ở bước hiện tại.
/// </summary>
public sealed record FunnelJourneyDto(Guid LeadId, string Code, string FullName, LeadSource Source, Guid BranchId, Guid? AssigneeId,
    string? AssigneeName, Guid? CampaignId, LeadStatus Status, DateTimeOffset CreatedAt, DateTimeOffset? CalledAt, DateTimeOffset? TestAt,
    DateTimeOffset? WonAt, FunnelStage Stage, bool Lost, int? StuckDays);

/// <summary>
/// CRM-18 — phễu chuyển đổi theo ngày tạo lead trong [<paramref name="From"/>, <paramref name="To"/>] (giờ VN), tính khi đọc;
/// phạm vi own = lead mình phụ trách (E1). "Test" = hẹn test / gửi link test / lịch test tại trung tâm (E2).
/// </summary>
public sealed record GetFunnelQuery(DateOnly? From, DateOnly? To, Guid? BranchId) : IQuery<IReadOnlyList<FunnelJourneyDto>>;

public sealed class GetFunnelHandler(ICrmDbContext db, CrmAccess access, PeopleNames people, IClock clock)
    : IQueryHandler<GetFunnelQuery, IReadOnlyList<FunnelJourneyDto>>
{
    /// <summary>Nằm ở bước quá số ngày này → kẹt</summary>
    private static readonly Dictionary<FunnelStage, int> StuckDays = new() { [FunnelStage.Lead] = 1, [FunnelStage.Called] = 7, [FunnelStage.Test] = 7 };

    public async Task<Result<IReadOnlyList<FunnelJourneyDto>>> Handle(GetFunnelQuery query, CancellationToken ct)
    {
        var vn = TimeSpan.FromHours(7);
        var toDay = query.To ?? clock.Today;
        var fromDay = query.From ?? toDay.AddDays(-29);
        if (fromDay > toDay) return Error.Validation("crm.funnel.invalid_range", "Ngày bắt đầu phải trước ngày kết thúc");
        var from = new DateTimeOffset(fromDay.ToDateTime(TimeOnly.MinValue), vn).ToUniversalTime();
        var to = new DateTimeOffset(toDay.AddDays(1).ToDateTime(TimeOnly.MinValue), vn).ToUniversalTime();
        var q = access.Scope(db.Leads.AsNoTracking(), PermissionAction.View, CrmAccess.Funnel)
            .Where(l => EF.Property<DateTimeOffset>(l, "CreatedAt") >= from && EF.Property<DateTimeOffset>(l, "CreatedAt") < to);
        if (query.BranchId is { } b) q = q.Where(l => l.BranchId == new BranchId(b));
        var rows = await q.Select(l => new { Lead = l, Created = EF.Property<DateTimeOffset>(l, "CreatedAt") }).ToListAsync(ct);
        if (rows.Count == 0) return Result.Success<IReadOnlyList<FunnelJourneyDto>>([]);

        var ids = rows.Select(r => r.Lead.Id).ToList();
        var attempts = (await db.PlacementAttempts.AsNoTracking().Where(a => ids.Contains(a.LeadId)).Select(a => new { a.LeadId, a.SentAt })
            .ToListAsync(ct)).ToLookup(a => a.LeadId, a => a.SentAt);
        var tests = (await db.Appointments.AsNoTracking()
                .Where(a => ids.Contains(a.LeadId) && a.Kind == AppointmentKind.Test && a.Status != AppointmentStatus.Cancelled)
                .Select(a => new { a.LeadId, Created = EF.Property<DateTimeOffset>(a, "CreatedAt") }).ToListAsync(ct))
            .ToLookup(a => a.LeadId, a => a.Created);
        var names = await people.OfAsync(rows.Select(r => r.Lead.AssigneeId), ct);
        var now = clock.UtcNow;

        return rows.Select(r =>
        {
            var l = r.Lead;
            var consults = l.Consultations.Where(c => !c.IsSystem).ToList();
            var called = consults.Where(c => c.Result != ConsultResult.NoAnswer).Select(c => (DateTimeOffset?)c.CreatedAt).Min()
                ?? (l.Status != LeadStatus.New ? l.LastContactAt : null);
            var test = consults.Where(c => c.Result == ConsultResult.TestBooked).Select(c => c.CreatedAt)
                .Concat(attempts[l.Id]).Concat(tests[l.Id]).Select(x => (DateTimeOffset?)x).Min();
            var won = l.Status == LeadStatus.Won
                ? l.Consultations.Where(c => c.Result == ConsultResult.Enrolled).Select(c => c.CreatedAt).Concat(l.StudentLinks.Select(k => k.CreatedAt))
                    .Select(x => (DateTimeOffset?)x).Min()
                : null;

            var reachedWon = l.Status == LeadStatus.Won;
            var reachedTest = reachedWon || l.Status == LeadStatus.TestScheduled || test is not null;
            var reachedCalled = reachedTest || called is not null || l.Status != LeadStatus.New;
            var stage = reachedWon ? FunnelStage.Won : reachedTest ? FunnelStage.Test : reachedCalled ? FunnelStage.Called : FunnelStage.Lead;
            var lost = l.Status == LeadStatus.Lost;

            int? stuck = null;
            if (!lost && stage != FunnelStage.Won && !l.Hidden)
            {
                var since = stage switch { FunnelStage.Called => called, FunnelStage.Test => test, _ => null } ?? r.Created;
                var days = (now - since).TotalHours / 24;
                if (days >= StuckDays[stage]) stuck = (int)Math.Floor(days);
            }
            return new FunnelJourneyDto(l.Id.Value, l.Code, l.FullName, l.Source, l.BranchId.Value, l.AssigneeId,
                l.AssigneeId is { } a ? names.GetValueOrDefault(a) : null, l.CampaignId?.Value, l.Status, r.Created, called, test, won, stage, lost, stuck);
        }).ToList();
    }
}
