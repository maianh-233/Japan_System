using JPCenter.Application.Common.Auditing;
using JPCenter.Application.Common.Messaging;
using JPCenter.Modules.Crm.Application.Abstractions;
using JPCenter.Modules.Crm.Application.LeadRules.Common;
using JPCenter.Modules.Crm.Domain.LeadRules;
using JPCenter.SharedKernel.Primitives;
using Microsoft.EntityFrameworkCore;

namespace JPCenter.Modules.Crm.Application.LeadRules.SaveLeadRule;

/// <summary>
/// CRM-09 bước 2 — thêm (<paramref name="Id"/> null, xếp cuối danh sách) / sửa quy tắc (chống sửa đè bằng <paramref name="Version"/>),
/// bật / tắt. Không Sale nào → E1; SLA ≤ 0 hoặc &gt; 7 ngày → E2.
/// </summary>
public sealed record SaveLeadRuleCommand(Guid? Id, LeadRuleSettings Settings, bool Active, uint Version) : ICommand<LeadRuleDto>;

public sealed class SaveLeadRuleHandler(ICrmDbContext db, LeadRuleReader reader, IAuditTrail audit) : ICommandHandler<SaveLeadRuleCommand, LeadRuleDto>
{
    public async Task<Result<LeadRuleDto>> Handle(SaveLeadRuleCommand cmd, CancellationToken ct)
    {
        LeadRule rule;
        if (cmd.Id is { } id)
        {
            var found = await db.LeadRules.FirstOrDefaultAsync(r => r.Id == new LeadRuleId(id), ct);
            if (found is null) return LeadRuleErrors.NotFound;
            if (cmd.Version != 0) ((DbContext)db).Entry(found).Property("xmin").OriginalValue = cmd.Version;
            if (found.Update(cmd.Settings) is { IsFailure: true } bad) return bad.Error;
            rule = found;
        }
        else
        {
            var last = await db.LeadRules.Select(r => (int?)r.Priority).MaxAsync(ct) ?? 0;
            var created = LeadRule.Create(cmd.Settings, last + 1);
            if (created.IsFailure) return created.Error;
            rule = created.Value;
            db.LeadRules.Add(rule);
        }
        if (cmd.Active) rule.Enable();
        else rule.Disable();

        await db.SaveChangesAsync(ct);
        audit.Record(AuditCategory.Data, AuditSeverity.Info, cmd.Id is null ? "Thêm quy tắc chia lead" : "Sửa quy tắc chia lead", rule.Name);
        return await reader.ToDtoAsync(rule, ct);
    }
}
