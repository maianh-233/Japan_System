using JPCenter.Application.Common.Auditing;
using JPCenter.Application.Common.Messaging;
using JPCenter.Modules.Crm.Application.Abstractions;
using JPCenter.Modules.Crm.Domain.LeadRules;
using JPCenter.SharedKernel.Primitives;
using Microsoft.EntityFrameworkCore;

namespace JPCenter.Modules.Crm.Application.LeadRules.DeleteLeadRule;

/// <summary>CRM-09.E4 — xóa mềm quy tắc; nhật ký chia lead cũ vẫn giữ tên quy tắc</summary>
public sealed record DeleteLeadRuleCommand(Guid Id) : ICommand;

public sealed class DeleteLeadRuleHandler(ICrmDbContext db, IAuditTrail audit) : ICommandHandler<DeleteLeadRuleCommand>
{
    public async Task<Result> Handle(DeleteLeadRuleCommand cmd, CancellationToken ct)
    {
        var rule = await db.LeadRules.FirstOrDefaultAsync(r => r.Id == new LeadRuleId(cmd.Id), ct);
        if (rule is null) return LeadRuleErrors.NotFound;
        db.LeadRules.Remove(rule);
        await db.SaveChangesAsync(ct);
        audit.Record(AuditCategory.Data, AuditSeverity.Warning, "Xóa quy tắc chia lead", rule.Name);
        return Result.Success();
    }
}
