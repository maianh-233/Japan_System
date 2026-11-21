using FluentValidation;
using JPCenter.Application.Common.Auditing;
using JPCenter.Application.Common.Messaging;
using JPCenter.Application.Common.Security;
using JPCenter.Modules.Crm.Application.Abstractions;
using JPCenter.Modules.Crm.Application.Common;
using JPCenter.Modules.Crm.Application.Leads.Common;
using JPCenter.Modules.Crm.Domain.Leads;
using JPCenter.SharedKernel.Primitives;

namespace JPCenter.Modules.Crm.Application.Leads.CreateLead;

/// <summary>
/// CRM-02 — thêm lead: sinh mã LD0001, không chọn Sale → chia tự động (CRM-10), Sale được giao nhận thông báo.
/// Trùng SĐT chỉ cảnh báo (E3). Phạm vi <c>own</c> (Sale tự nhập) → lead về chính mình.
/// </summary>
public sealed record CreateLeadCommand(LeadInput Input, LeadStatus Status, string? LostReason) : ICommand<LeadSaved>;

public sealed class CreateLeadValidator : AbstractValidator<CreateLeadCommand>
{
    public CreateLeadValidator() => RuleFor(x => x.Input).NotNull();
}

public sealed class CreateLeadHandler(ICrmDbContext db, CrmAccess access, LeadChecks checks, LeadCodes codes, LeadReader reader,
    LeadRouting routing, IAuditTrail audit) : ICommandHandler<CreateLeadCommand, LeadSaved>
{
    public async Task<Result<LeadSaved>> Handle(CreateLeadCommand cmd, CancellationToken ct)
    {
        var input = cmd.Input;
        if (!access.InBranch(CrmAccess.Leads, PermissionAction.Create, input.BranchId)) return CrmAccess.BranchNotAllowed;
        var assignee = access.OwnOnly(CrmAccess.Leads, PermissionAction.Create) ?? input.AssigneeId;
        if (await checks.CheckAsync(input, newBranch: true, assignee, ct) is { IsFailure: true } invalid) return invalid.Error;

        var created = Lead.Create(Lead.CodeFor(await codes.NextNumberAsync(ct)), input.ToInfo(), cmd.Status, cmd.LostReason);
        if (created.IsFailure) return created.Error;
        var lead = created.Value;
        lead.SetNotifyChannels(input.NotifyChannels ?? []);
        var warnings = await reader.DuplicatesAsync(lead.Phone, null, ct);

        db.Leads.Add(lead);
        await routing.AssignNewAsync(lead, assignee, access.ActorName, ct);
        await db.SaveChangesAsync(ct);
        audit.Record(AuditCategory.Data, AuditSeverity.Info, "Thêm lead", $"{lead.Code} — {lead.FullName}", branchId: lead.BranchId.Value);
        return new LeadSaved(await reader.ToDtoAsync(lead, ct), warnings);
    }
}
