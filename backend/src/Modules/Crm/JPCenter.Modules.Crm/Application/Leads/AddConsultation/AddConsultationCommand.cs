using JPCenter.Application.Common.Messaging;
using JPCenter.Modules.Crm.Application.Abstractions;
using JPCenter.Modules.Crm.Application.Common;
using JPCenter.Modules.Crm.Application.Leads.Common;
using JPCenter.Modules.Crm.Domain.Leads;
using JPCenter.SharedKernel.Domain;
using JPCenter.SharedKernel.Primitives;

namespace JPCenter.Modules.Crm.Application.Leads.AddConsultation;

/// <summary>
/// CRM-05 — ghi 1 lần tư vấn (chỉ thêm — E4): lần liên hệ cuối = bây giờ, hẹn gọi lại, Mới → Đang tư vấn (dừng SLA — E5),
/// "Hẹn test" → Hẹn test (E2). Hẹn gọi lại không trước hôm nay.
/// </summary>
public sealed record AddConsultationCommand(Guid LeadId, ConsultChannel Channel, ConsultResult Result, string? Content,
    DateTimeOffset? NextFollowUpAt) : ICommand<LeadDetailDto>;

public sealed class AddConsultationHandler(ICrmDbContext db, CrmAccess access, LeadEditor editor, LeadReader reader, IClock clock)
    : ICommandHandler<AddConsultationCommand, LeadDetailDto>
{
    public async Task<Result<LeadDetailDto>> Handle(AddConsultationCommand cmd, CancellationToken ct)
    {
        var lead = await editor.LoadAsync(cmd.LeadId, ct);
        if (lead is null) return LeadErrors.NotFound;
        if (cmd.NextFollowUpAt is { } follow && DateOnly.FromDateTime(follow.ToOffset(TimeSpan.FromHours(7)).DateTime) < clock.Today)
            return LeadChecks.FollowUpInPast;
        if (lead.AddConsultation(cmd.Channel, cmd.Result, cmd.Content ?? "", access.ActorId, access.ActorName, clock.UtcNow,
                cmd.NextFollowUpAt) is { IsFailure: true } bad)
            return bad.Error;
        await db.SaveChangesAsync(ct);
        return await reader.DetailAsync(lead, ct);
    }
}
