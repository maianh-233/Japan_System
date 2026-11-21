using JPCenter.Modules.Crm.Application.Abstractions;
using JPCenter.Modules.Crm.Domain.LeadRules;
using JPCenter.Modules.Crm.Domain.Leads;
using JPCenter.SharedKernel.Domain;
using Microsoft.EntityFrameworkCore;

namespace JPCenter.Modules.Crm.Application.Common;

/// <summary>
/// CRM-10 — chia lead mới chưa có Sale theo quy tắc (domain <see cref="LeadRouter"/>), dời con trỏ xoay vòng, ghi nhật ký + dòng hệ thống,
/// báo Sale được giao (hoặc báo quản lý khi chưa chia được). Chọn tay → chỉ ghi nhật ký "Chọn tay" (E4). Handler gọi rồi lưu 1 lần.
/// </summary>
public sealed class LeadRouting(ICrmDbContext db, SalesTeam team, CrmNotices notices, IClock clock)
{
    private List<LeadRule>? _rules;
    private RoutingContext? _ctx;

    /// <summary>Lead vừa tạo (đã Add vào context)</summary>
    public async Task<RouteDecision> AssignNewAsync(Lead lead, Guid? chosenSale, string chosenBy, CancellationToken ct)
    {
        var now = clock.UtcNow;
        if (chosenSale is { } sale)
        {
            lead.AssignTo(sale);
            var reason = $"Chọn tay — {chosenBy}";
            db.LeadAssignmentLogs.Add(LeadAssignmentLog.Create(lead.Id, sale, null, reason, now));
            notices.LeadAssigned(lead.Id.Value, lead.FullName, lead.Code, sale, "Được giao trực tiếp");
            return new RouteDecision(sale, null, null, reason);
        }

        _rules ??= await db.LeadRules.Where(r => r.Active).ToListAsync(ct);
        _ctx ??= await team.ContextAsync(ct);
        var d = LeadRouter.Route(_rules, lead.Source, lead.BranchId, lead.Interest, _ctx);
        db.LeadAssignmentLogs.Add(LeadAssignmentLog.Create(lead.Id, d.SaleId, d.Rule?.Id, d.Reason, now));
        lead.AddSystemNote($"Chia lead: {d.Reason}", now);
        if (d.Cursor is { } cursor) d.Rule!.MoveCursor(cursor);
        if (d.SaleId is { } chosen)
        {
            lead.AssignTo(chosen);
            notices.LeadAssigned(lead.Id.Value, lead.FullName, lead.Code, chosen, d.Reason);
            // Các lead sau trong cùng lô (nhập Excel) thấy tải mới
            var load = _ctx.OpenLoad;
            var bumped = new Dictionary<Guid, int> { [chosen] = load(chosen) + 1 };
            _ctx = _ctx with { OpenLoad = id => bumped.TryGetValue(id, out var n) ? n : load(id) };
        }
        else notices.LeadUnassigned(lead.Id.Value, lead.FullName, lead.BranchId.Value, d.Reason);
        return d;
    }
}
