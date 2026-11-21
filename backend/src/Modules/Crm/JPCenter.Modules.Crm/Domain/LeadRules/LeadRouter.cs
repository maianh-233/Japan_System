using JPCenter.Modules.Crm.Domain.Leads;
using JPCenter.SharedKernel.Primitives;

namespace JPCenter.Modules.Crm.Domain.LeadRules;

/// <summary>
/// Bộ máy CHIA LEAD (CRM-10) — thuần, giống <c>frontend/src/features/crm/admission/routing.ts</c>.
/// Application nạp quy tắc + ngữ cảnh, gọi <see cref="Route"/>, rồi giao Sale, dời con trỏ và ghi nhật ký.
/// </summary>
public static class LeadRouter
{
    /// <summary>Quy tắc đầu tiên (đang bật, theo thứ tự ưu tiên) khớp lead</summary>
    public static LeadRule? MatchRule(IEnumerable<LeadRule> rules, LeadSource source, BranchId branchId, string? interest) =>
        rules.Where(r => r.Active).OrderBy(r => r.Priority).FirstOrDefault(r => r.Matches(source, branchId, interest));

    public static RouteDecision Route(IEnumerable<LeadRule> rules, LeadSource source, BranchId branchId, string? interest, RoutingContext ctx)
    {
        var rule = MatchRule(rules, source, branchId, interest);
        if (rule is null) return new(null, null, null, "Không quy tắc nào khớp — chờ quản lý giao tay");

        // Sale đã nghỉ / chuyển phòng tự bị bỏ qua dù còn trong quy tắc (CRM-09.E3)
        var team = rule.SaleIds.Where(ctx.IsWorking).ToList();
        if (team.Count == 0) return new(null, rule, null, $"Quy tắc \"{rule.Name}\" không còn Sale nào đang làm việc");

        var pool = team;
        var notes = new List<string>();
        if (rule.OnlyOnShift)
        {
            var on = team.Where(ctx.IsOnShift).ToList();
            if (on.Count > 0)
            {
                pool = on;
                notes.Add($"{on.Count}/{team.Count} Sale đang trong ca");
            }
            else notes.Add("không ai trong ca → chia cho cả nhóm");
        }

        var saleId = pool[0];
        int? cursor = null;
        switch (rule.Method)
        {
            case RouteMethod.RoundRobin:
                // Đi tiếp từ vị trí sau con trỏ, bỏ qua người không nằm trong pool
                for (var k = 1; k <= team.Count; k++)
                {
                    var idx = (((rule.Cursor + k) % team.Count) + team.Count) % team.Count;
                    if (!pool.Contains(team[idx])) continue;
                    (cursor, saleId) = (idx, team[idx]);
                    break;
                }
                break;
            case RouteMethod.LeastLoad:
                saleId = LeastLoaded(pool, ctx);
                notes.Add($"đang giữ {ctx.OpenLoad(saleId)} lead mở — ít nhất");
                break;
        }

        var note = notes.Count > 0 ? $" ({string.Join(", ", notes)})" : "";
        return new(saleId, rule, cursor, $"Quy tắc \"{rule.Name}\" → {ctx.NameOf(saleId)}{note}");
    }

    /// <summary>
    /// CRM-11 — Sale thay thế khi quá 2×SLA: cùng nhóm, còn làm việc, khác người đang giữ; ưu tiên trong ca rồi ít lead mở nhất.
    /// Null = không có ai thay (CRM-11.E2).
    /// </summary>
    public static Guid? PickReplacement(LeadRule rule, Guid? currentSaleId, RoutingContext ctx)
    {
        var team = rule.SaleIds.Where(id => id != currentSaleId && ctx.IsWorking(id)).ToList();
        if (team.Count == 0) return null;
        var onShift = team.Where(ctx.IsOnShift).ToList();
        return LeastLoaded(onShift.Count > 0 ? onShift : team, ctx);
    }

    /// <summary>Ít lead mở nhất; bằng nhau → người đứng trước trong nhóm</summary>
    private static Guid LeastLoaded(List<Guid> pool, RoutingContext ctx) =>
        pool.Select((id, i) => (id, i, load: ctx.OpenLoad(id))).OrderBy(x => x.load).ThenBy(x => x.i).First().id;
}
