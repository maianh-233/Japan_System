using JPCenter.Modules.Crm.Application.Abstractions;
using JPCenter.Modules.Crm.Application.Common;
using JPCenter.Modules.Crm.Domain.LeadRules;
using JPCenter.Modules.Hr.Contracts;
using Microsoft.EntityFrameworkCore;

namespace JPCenter.Modules.Crm.Application.LeadRules.Common;

/// <summary>Dựng DTO quy tắc + cảnh báo trên quy tắc: Sale đã nghỉ / chuyển phòng / nghỉ dài ngày bị bỏ qua khi chia (CRM-09.E3)</summary>
public sealed class LeadRuleReader(ICrmDbContext db, IEmployeeDirectory employees)
{
    public async Task<IReadOnlyList<LeadRuleDto>> ToDtosAsync(IReadOnlyList<LeadRule> rules, CancellationToken ct)
    {
        if (rules.Count == 0) return [];
        var ids = rules.Select(r => r.Id).ToList();
        var versions = await db.LeadRules.AsNoTracking().Where(r => ids.Contains(r.Id))
            .Select(r => new { r.Id, Version = EF.Property<uint>(r, "xmin") }).ToDictionaryAsync(x => x.Id, x => x.Version, ct);
        var people = await employees.FindManyAsync(rules.SelectMany(r => r.SaleIds).Distinct().ToList(), ct);
        return rules.Select(r =>
        {
            var warnings = r.SaleIds.Where(id => !people.TryGetValue(id, out var e) || !SalesTeam.IsWorkingSale(e))
                .Select(id => people.TryGetValue(id, out var e)
                    ? $"{e.FullName} {(e.HasQuit ? "đã nghỉ việc" : e.Status == "leave" ? "đang nghỉ dài ngày" : "không còn ở phòng Sale")} — bị bỏ qua khi chia"
                    : "1 Sale không còn trong hệ thống — bị bỏ qua khi chia")
                .ToList();
            if (warnings.Count == r.SaleIds.Count) warnings.Add("Quy tắc không còn Sale nào đang làm việc");
            return new LeadRuleDto(r.Id.Value, r.Name, r.Priority, r.Active, r.Sources, r.BranchIds.Select(b => b.Value).ToList(), r.Interests,
                r.Method, r.SaleIds, r.OnlyOnShift, r.SlaMinutes, r.EscalateToIds, r.ReassignOnBreach, r.Cursor, warnings,
                versions.GetValueOrDefault(r.Id));
        }).ToList();
    }

    public async Task<LeadRuleDto> ToDtoAsync(LeadRule rule, CancellationToken ct) => (await ToDtosAsync([rule], ct))[0];
}
