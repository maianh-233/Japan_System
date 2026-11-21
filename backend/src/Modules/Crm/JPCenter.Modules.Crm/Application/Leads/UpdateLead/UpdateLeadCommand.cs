using JPCenter.Application.Common.Auditing;
using JPCenter.Application.Common.Messaging;
using JPCenter.Application.Common.Security;
using JPCenter.Modules.Crm.Application.Abstractions;
using JPCenter.Modules.Crm.Application.Common;
using JPCenter.Modules.Crm.Application.Leads.Common;
using JPCenter.Modules.Crm.Domain.LeadRules;
using JPCenter.Modules.Crm.Domain.Leads;
using JPCenter.SharedKernel.Domain;
using JPCenter.SharedKernel.Primitives;

namespace JPCenter.Modules.Crm.Application.Leads.UpdateLead;

/// <summary>
/// CRM-04 — sửa thông tin lead (lead ẩn vẫn sửa, vẫn ẩn — E4), chống sửa đè bằng <paramref name="Version"/> (E5).
/// Đổi Sale phụ trách → nhật ký chia lead "Chuyển tay" + báo Sale mới.
/// </summary>
public sealed record UpdateLeadCommand(Guid Id, LeadInput Input, uint Version) : ICommand<LeadSaved>;

public sealed class UpdateLeadHandler(ICrmDbContext db, CrmAccess access, LeadEditor editor, LeadChecks checks, LeadReader reader,
    CrmNotices notices, IClock clock, IAuditTrail audit) : ICommandHandler<UpdateLeadCommand, LeadSaved>
{
    public async Task<Result<LeadSaved>> Handle(UpdateLeadCommand cmd, CancellationToken ct)
    {
        var lead = await editor.LoadAsync(cmd.Id, ct, cmd.Version);
        if (lead is null) return LeadErrors.NotFound;
        var input = cmd.Input;
        var branchChanged = input.BranchId != lead.BranchId.Value;
        if (branchChanged && !access.InBranch(CrmAccess.Leads, PermissionAction.Update, input.BranchId)) return CrmAccess.BranchNotAllowed;
        var newSale = input.AssigneeId != lead.AssigneeId ? input.AssigneeId : null;
        if (await checks.CheckAsync(input, branchChanged, newSale, ct) is { IsFailure: true } invalid) return invalid.Error;

        if (lead.Update(input.ToInfo()) is { IsFailure: true } bad) return bad.Error;
        lead.SetNotifyChannels(input.NotifyChannels ?? []);
        if (input.AssigneeId != lead.AssigneeId)
        {
            lead.AssignTo(input.AssigneeId);
            var reason = $"Chuyển tay — {access.ActorName}";
            db.LeadAssignmentLogs.Add(LeadAssignmentLog.Create(lead.Id, input.AssigneeId, null, reason, clock.UtcNow));
            if (input.AssigneeId is { } sale) notices.LeadAssigned(lead.Id.Value, lead.FullName, lead.Code, sale, reason);
        }

        await db.SaveChangesAsync(ct);
        audit.Record(AuditCategory.Data, AuditSeverity.Info, "Sửa lead", $"{lead.Code} — {lead.FullName}", branchId: lead.BranchId.Value);
        return new LeadSaved(await reader.ToDtoAsync(lead, ct), await reader.DuplicatesAsync(lead.Phone, lead.Id, ct));
    }
}
