using JPCenter.Application.Common.Auditing;
using JPCenter.Application.Common.Messaging;
using JPCenter.Modules.Crm.Application.Abstractions;
using JPCenter.Modules.Crm.Application.Leads.Common;
using JPCenter.Modules.Crm.Domain.Leads;
using JPCenter.SharedKernel.Domain;
using JPCenter.SharedKernel.Primitives;

namespace JPCenter.Modules.Crm.Application.Leads.SetLeadHidden;

/// <summary>
/// CRM-06 — ẩn / bỏ ẩn lead (không xóa). HV liên kết, lịch sử tư vấn, tài khoản cổng PH giữ nguyên (E2). Ẩn lại lead đã ẩn → E1.
/// </summary>
public sealed record SetLeadHiddenCommand(Guid Id, bool Hidden) : ICommand<LeadDto>;

public sealed class SetLeadHiddenHandler(ICrmDbContext db, LeadEditor editor, LeadReader reader, IClock clock, IAuditTrail audit)
    : ICommandHandler<SetLeadHiddenCommand, LeadDto>
{
    public async Task<Result<LeadDto>> Handle(SetLeadHiddenCommand cmd, CancellationToken ct)
    {
        var lead = await editor.LoadAsync(cmd.Id, ct);
        if (lead is null) return LeadErrors.NotFound;
        if (cmd.Hidden)
        {
            if (lead.Hide(clock.UtcNow) is { IsFailure: true } bad) return bad.Error;
        }
        else lead.Unhide();
        await db.SaveChangesAsync(ct);
        audit.Record(AuditCategory.Data, AuditSeverity.Info, cmd.Hidden ? "Ẩn lead" : "Bỏ ẩn lead", $"{lead.Code} — {lead.FullName}",
            branchId: lead.BranchId.Value);
        return await reader.ToDtoAsync(lead, ct);
    }
}
