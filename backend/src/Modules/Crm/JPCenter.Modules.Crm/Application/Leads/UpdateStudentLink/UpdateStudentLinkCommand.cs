using JPCenter.Application.Common.Messaging;
using JPCenter.Modules.Crm.Application.Abstractions;
using JPCenter.Modules.Crm.Application.Leads.Common;
using JPCenter.Modules.Crm.Application.Leads.LinkStudent;
using JPCenter.Modules.Crm.Domain.Leads;
using JPCenter.SharedKernel.Primitives;

namespace JPCenter.Modules.Crm.Application.Leads.UpdateStudentLink;

/// <summary>CRM-08 bước 2 — đổi quan hệ / cờ liên hệ chính · nhận hóa đơn · nhận thông báo (E2 tự tắt liên hệ chính ở lead khác, E3 / E4 cảnh báo).</summary>
public sealed record UpdateStudentLinkCommand(Guid LeadId, Guid StudentId, Relationship Relationship, bool Primary, bool Billing, bool Notify)
    : ICommand<LeadLinksResult>;

public sealed class UpdateStudentLinkHandler(ICrmDbContext db, LeadEditor editor, LeadReader reader, GuardianLinks links)
    : ICommandHandler<UpdateStudentLinkCommand, LeadLinksResult>
{
    public async Task<Result<LeadLinksResult>> Handle(UpdateStudentLinkCommand cmd, CancellationToken ct)
    {
        var lead = await editor.LoadAsync(cmd.LeadId, ct);
        if (lead is null) return LeadErrors.NotFound;
        var wasPrimary = lead.StudentLinks.FirstOrDefault(k => k.StudentId == cmd.StudentId)?.IsPrimary ?? false;
        if (lead.SetLinkFlags(cmd.StudentId, cmd.Relationship, cmd.Primary, cmd.Billing, cmd.Notify) is { IsFailure: true } bad) return bad.Error;
        var warnings = new List<LeadWarning>();
        if (cmd.Primary && !wasPrimary && await links.MakePrimaryAsync(lead, cmd.StudentId, ct) is { } changed) warnings.Add(changed);
        await db.SaveChangesAsync(ct);
        warnings.AddRange(await links.CheckStudentAsync(cmd.StudentId, ct));
        return new LeadLinksResult(await reader.LinksAsync(lead, ct), warnings);
    }
}
