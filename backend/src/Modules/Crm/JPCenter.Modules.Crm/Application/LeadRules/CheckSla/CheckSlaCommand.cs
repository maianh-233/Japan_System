using JPCenter.Application.Common.Messaging;
using JPCenter.Modules.Crm.Application.Abstractions;
using JPCenter.Modules.Crm.Application.Common;
using JPCenter.Modules.Crm.Domain.LeadRules;
using JPCenter.Modules.Crm.Domain.Leads;
using JPCenter.Modules.System.Contracts;
using JPCenter.SharedKernel.Domain;
using JPCenter.SharedKernel.Primitives;
using Microsoft.EntityFrameworkCore;

namespace JPCenter.Modules.Crm.Application.LeadRules.CheckSla;

public sealed record SlaRunResult(int Escalated, int Reassigned);

/// <summary>
/// CRM-11 — job mỗi phút: lead không ẩn, còn Mới, chưa ai liên hệ, quá SLA → cảnh báo 1 lần (người "quá hạn báo cho", không có → người
/// quản lý chia lead của chi nhánh); quy tắc bật tự chuyển + quá 2×SLA → chuyển Sale khác cùng nhóm 1 lần (không có ai → chỉ cảnh báo — E2).
/// Hơn 3 lead cùng lượt → gom 1 thông báo. Chạy lại an toàn nhờ 2 mốc trên lead (E3).
/// </summary>
public sealed record CheckSlaCommand : ICommand<SlaRunResult>;

public sealed class CheckSlaHandler(ICrmDbContext db, SalesTeam team, ICenterSettings settings, CrmNotices notices, IClock clock)
    : ICommandHandler<CheckSlaCommand, SlaRunResult>
{
    public const int BatchLimit = 3;

    public async Task<Result<SlaRunResult>> Handle(CheckSlaCommand cmd, CancellationToken ct)
    {
        var now = clock.UtcNow;
        var candidates = await db.Leads
            .Where(l => !l.Hidden && l.Status == LeadStatus.New && l.LastContactAt == null && (l.SlaEscalatedAt == null || l.SlaReassignedAt == null))
            .Select(l => new { Lead = l, Created = EF.Property<DateTimeOffset>(l, "CreatedAt") })
            .ToListAsync(ct);
        if (candidates.Count == 0) return new SlaRunResult(0, 0);

        var rules = await db.LeadRules.AsNoTracking().Where(r => r.Active).ToListAsync(ct);
        var fallback = (await settings.GetAsync(ct)).CrmDefaultSlaMinutes;
        RoutingContext? ctx = null;
        var escalated = new List<(Lead Lead, int Late, LeadRule? Rule)>();
        var reassigned = new List<(Lead Lead, Guid? From, Guid To, int Sla)>();

        foreach (var c in candidates)
        {
            var lead = c.Lead;
            var rule = LeadRouter.MatchRule(rules, lead.Source, lead.BranchId, lead.Interest);
            var check = SlaCheck.Of(lead, c.Created, rule, fallback, now);
            if (!check.Overdue) continue;
            if (check.Escalate && lead.MarkSlaEscalated(now)) escalated.Add((lead, check.LateMinutes, rule));
            if (!check.Reassign) continue;

            ctx ??= await team.ContextAsync(ct);
            if (LeadRouter.PickReplacement(rule!, lead.AssigneeId, ctx) is not { } next) continue;
            var from = lead.AssigneeId;
            if (lead.ReassignForSla(next, now).IsFailure) continue;
            var reason = $"Tự chuyển do quá 2×SLA ({check.SlaMinutes * 2} phút) — trước: {(from is { } f ? ctx.NameOf(f) : "chưa giao")}";
            db.LeadAssignmentLogs.Add(LeadAssignmentLog.Create(lead.Id, next, rule!.Id, reason, now));
            lead.AddSystemNote(reason, now);
            reassigned.Add((lead, from, next, check.SlaMinutes));
        }

        Notify(escalated, reassigned, ctx);
        await db.SaveChangesAsync(ct);
        return new SlaRunResult(escalated.Count, reassigned.Count);
    }

    private void Notify(List<(Lead Lead, int Late, LeadRule? Rule)> escalated, List<(Lead Lead, Guid? From, Guid To, int Sla)> reassigned,
        RoutingContext? ctx)
    {
        if (escalated.Count > BatchLimit)
        {
            var to = escalated.SelectMany(e => e.Rule?.EscalateToIds ?? []).Distinct().ToList();
            var branches = escalated.Select(e => e.Lead.BranchId.Value).Distinct().ToList();
            notices.SlaBreached($"{escalated.Count} lead quá hạn liên hệ", "Chưa ai gọi trong thời hạn SLA — xem trang Lead, lọc \"Quá SLA\".",
                "/admin/crm/leads?slaOverdue=true", to, branches.Count == 1 ? branches[0] : null);
        }
        else
            foreach (var (lead, late, rule) in escalated)
                notices.SlaBreached($"Lead quá SLA: {lead.FullName}",
                    $"{lead.Code} · {(lead.AssigneeId is null ? "chưa giao Sale" : "chưa liên hệ")} · trễ {LateLabel(late)}",
                    CrmNotices.LeadLink(lead.Id.Value), rule?.EscalateToIds ?? [], lead.BranchId.Value);

        if (reassigned.Count > BatchLimit)
            notices.SlaReassigned($"Đã tự chuyển {reassigned.Count} lead quá hạn",
                "Quá gấp đôi SLA mà chưa liên hệ — đã giao cho Sale khác trong nhóm. Xem nhật ký chia lead.", CrmNotices.RulesLink,
                reassigned.Select(r => r.To).Distinct().ToList(), null);
        else
            foreach (var (lead, from, to, _) in reassigned)
                notices.SlaReassigned($"Đã chuyển lead {lead.FullName}",
                    $"{(from is { } f ? ctx!.NameOf(f) : "Chưa giao")} → {ctx!.NameOf(to)} (quá gấp đôi SLA chưa liên hệ)",
                    CrmNotices.LeadLink(lead.Id.Value), [to], lead.BranchId.Value);
    }

    private static string LateLabel(int minutes) =>
        minutes >= 60 ? $"{minutes / 60} giờ{(minutes % 60 > 0 ? $" {minutes % 60} phút" : "")}" : $"{minutes} phút";
}
