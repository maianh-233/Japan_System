using JPCenter.Application.Common.Auditing;
using JPCenter.Application.Common.Messaging;
using JPCenter.Modules.Crm.Application.Abstractions;
using JPCenter.Modules.Crm.Application.Leads.Common;
using JPCenter.Modules.Crm.Domain.Leads;
using JPCenter.SharedKernel.Domain;
using JPCenter.SharedKernel.Primitives;

namespace JPCenter.Modules.Crm.Application.Leads.ChangeLeadStatus;

/// <summary>
/// CRM-04 — chuyển giai đoạn (kéo thả Kanban): "Mất" cần lý do (E1), "Đã chốt" chỉ qua ghi danh (E2), mở lại lead mất (E3),
/// lead đã chốt khóa giai đoạn.
/// </summary>
public sealed record ChangeLeadStatusCommand(Guid Id, LeadStatus Status, string? LostReason) : ICommand<LeadDto>;

public sealed class ChangeLeadStatusHandler(ICrmDbContext db, LeadEditor editor, LeadReader reader, IClock clock, IAuditTrail audit)
    : ICommandHandler<ChangeLeadStatusCommand, LeadDto>
{
    public async Task<Result<LeadDto>> Handle(ChangeLeadStatusCommand cmd, CancellationToken ct)
    {
        var lead = await editor.LoadAsync(cmd.Id, ct);
        if (lead is null) return LeadErrors.NotFound;
        var before = lead.Status;
        if (lead.ChangeStatus(cmd.Status, cmd.LostReason, clock.UtcNow) is { IsFailure: true } bad) return bad.Error;
        await db.SaveChangesAsync(ct);
        if (before != lead.Status)
            audit.Record(AuditCategory.Data, AuditSeverity.Info, "Đổi giai đoạn lead", $"{lead.Code} — {lead.FullName}", $"{before} → {lead.Status}",
                branchId: lead.BranchId.Value);
        return await reader.ToDtoAsync(lead, ct);
    }
}
