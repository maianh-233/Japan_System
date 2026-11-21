using JPCenter.Application.Common.Auditing;
using JPCenter.Application.Common.Messaging;
using JPCenter.Modules.Crm.Application.Abstractions;
using JPCenter.Modules.Crm.Application.Common;
using JPCenter.Modules.Crm.Application.Leads.Common;
using JPCenter.Modules.Crm.Domain.LeadRules;
using JPCenter.Modules.Crm.Domain.Leads;
using JPCenter.SharedKernel.Domain;
using JPCenter.SharedKernel.Primitives;

namespace JPCenter.Modules.Crm.Application.Leads.AssignLead;

/// <summary>
/// CRM-04 / CRM-11 bước 4 — giao / chuyển Sale bằng tay. Sale phải còn làm, cùng chi nhánh (CRM-02.E7). Lead đang quá SLA →
/// nhật ký "Chuyển tay do quá SLA".
/// </summary>
public sealed record AssignLeadCommand(Guid Id, Guid SaleId, string? Note) : ICommand<LeadDto>;

public sealed class AssignLeadHandler(ICrmDbContext db, CrmAccess access, LeadEditor editor, LeadReader reader, SalesTeam team,
    CrmNotices notices, IClock clock, IAuditTrail audit) : ICommandHandler<AssignLeadCommand, LeadDto>
{
    public async Task<Result<LeadDto>> Handle(AssignLeadCommand cmd, CancellationToken ct)
    {
        var lead = await editor.LoadAsync(cmd.Id, ct);
        if (lead is null) return LeadErrors.NotFound;
        if (lead.AssigneeId == cmd.SaleId) return await reader.ToDtoAsync(lead, ct);
        if (!await team.CanTakeAsync(cmd.SaleId, lead.BranchId.Value, ct)) return LeadErrors.InvalidAssignee;

        var meta = await reader.MetaAsync([lead.Id], ct);
        var (sla, _) = (await reader.SlaOfAsync(ct))(lead);
        var overdue = lead.IsSlaOverdue(meta[lead.Id].Created, sla, clock.UtcNow);
        var reason = (overdue ? "Chuyển tay do quá SLA" : "Chuyển tay") + $" — {access.ActorName}"
            + (string.IsNullOrWhiteSpace(cmd.Note) ? "" : $": {cmd.Note.Trim()}");

        lead.AssignTo(cmd.SaleId);
        db.LeadAssignmentLogs.Add(LeadAssignmentLog.Create(lead.Id, cmd.SaleId, null, reason, clock.UtcNow));
        notices.LeadAssigned(lead.Id.Value, lead.FullName, lead.Code, cmd.SaleId, reason);
        await db.SaveChangesAsync(ct);
        audit.Record(AuditCategory.Data, AuditSeverity.Info, "Chuyển Sale phụ trách lead", $"{lead.Code} — {lead.FullName}", reason,
            branchId: lead.BranchId.Value);
        return await reader.ToDtoAsync(lead, ct);
    }
}
