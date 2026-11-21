using JPCenter.Application.Common.Messaging;
using JPCenter.Modules.Crm.Application.Abstractions;
using JPCenter.Modules.Crm.Application.LeadRules.Common;
using JPCenter.Modules.Crm.Domain.LeadRules;
using JPCenter.SharedKernel.Primitives;
using Microsoft.EntityFrameworkCore;

namespace JPCenter.Modules.Crm.Application.LeadRules.ReorderLeadRules;

/// <summary>CRM-09.E5 — đổi thứ tự ưu tiên: 1 request gửi cả danh sách Id theo thứ tự mới (phải đủ, không trùng)</summary>
public sealed record ReorderLeadRulesCommand(List<Guid> Ids) : ICommand<IReadOnlyList<LeadRuleDto>>;

public sealed class ReorderLeadRulesHandler(ICrmDbContext db, LeadRuleReader reader) : ICommandHandler<ReorderLeadRulesCommand, IReadOnlyList<LeadRuleDto>>
{
    public async Task<Result<IReadOnlyList<LeadRuleDto>>> Handle(ReorderLeadRulesCommand cmd, CancellationToken ct)
    {
        var rules = await db.LeadRules.ToListAsync(ct);
        if (cmd.Ids.Count != rules.Count || cmd.Ids.Distinct().Count() != cmd.Ids.Count || rules.Any(r => !cmd.Ids.Contains(r.Id.Value)))
            return LeadRuleErrors.InvalidOrder;
        foreach (var r in rules) r.SetPriority(cmd.Ids.IndexOf(r.Id.Value) + 1);
        await db.SaveChangesAsync(ct);
        return Result.Success(await reader.ToDtosAsync(rules.OrderBy(r => r.Priority).ToList(), ct));
    }
}
