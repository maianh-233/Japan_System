using JPCenter.Application.Common.Auditing;
using JPCenter.Application.Common.Messaging;
using JPCenter.Application.Common.Security;
using JPCenter.Modules.Crm.Application.Abstractions;
using JPCenter.Modules.Crm.Application.Common;
using JPCenter.Modules.Crm.Application.Guardians.Common;
using JPCenter.Modules.Crm.Application.Leads.Common;
using JPCenter.Modules.Crm.Domain.Leads;
using JPCenter.SharedKernel.Primitives;

namespace JPCenter.Modules.Crm.Application.Guardians.UpdateGuardian;

/// <summary>
/// HV-12 bước 4 — sửa thông tin liên hệ (họ tên, SĐT, email, chi nhánh, ghi chú) + kênh nhận thông báo. Giữ nguồn / giai đoạn / Sale.
/// Trùng SĐT chỉ cảnh báo (như CRM-04); chống sửa đè bằng <paramref name="Version"/>.
/// </summary>
public sealed record UpdateGuardianCommand(Guid Id, string? FullName, string? Phone, string? Email, Guid BranchId, List<ContactChannel>? NotifyChannels,
    string? Note, uint Version) : ICommand<GuardianSaved>;

public sealed class UpdateGuardianHandler(ICrmDbContext db, GuardianAccess access, LeadReader reader, IAuditTrail audit)
    : ICommandHandler<UpdateGuardianCommand, GuardianSaved>
{
    public async Task<Result<GuardianSaved>> Handle(UpdateGuardianCommand cmd, CancellationToken ct)
    {
        var lead = await access.LoadAsync(cmd.Id, PermissionAction.Update, ct, cmd.Version);
        if (lead is null) return LeadErrors.NotFound;
        if (cmd.BranchId != lead.BranchId.Value && !access.InBranch(PermissionAction.Update, cmd.BranchId)) return CrmAccess.BranchNotAllowed;
        var info = new LeadInfo(cmd.FullName ?? "", cmd.Phone ?? "", cmd.Email, lead.Source, new BranchId(cmd.BranchId), lead.Interest, cmd.Note,
            lead.CampaignId, lead.NextFollowUpAt);
        if (lead.Update(info) is { IsFailure: true } bad) return bad.Error;
        if (cmd.NotifyChannels is not null) lead.SetNotifyChannels(cmd.NotifyChannels);
        await db.SaveChangesAsync(ct);
        audit.Record(AuditCategory.Data, AuditSeverity.Info, "Sửa phụ huynh / người giám hộ", $"{lead.Code} — {lead.FullName}", branchId: lead.BranchId.Value);
        var meta = (await reader.MetaAsync([lead.Id], ct))[lead.Id];
        return new GuardianSaved(GuardianMapping.ToDto(lead, meta.Created, meta.Version), false, await reader.DuplicatesAsync(lead.Phone, lead.Id, ct));
    }
}
