using JPCenter.Modules.Crm.Domain.Campaigns;
using JPCenter.Modules.Crm.Domain.Leads;
using JPCenter.SharedKernel.Primitives;

namespace JPCenter.Modules.Crm.Application.Leads.Common;

/// <summary>Ô nhập của form lead (CRM-02 / CRM-04) — khớp <c>LeadFormFields</c> của frontend.</summary>
public sealed record LeadInput(
    string? FullName,
    string? Phone,
    string? Email,
    LeadSource Source,
    Guid BranchId,
    string? Interest,
    string? Note,
    Guid? CampaignId,
    Guid? AssigneeId,
    DateTimeOffset? NextFollowUpAt,
    List<ContactChannel>? NotifyChannels)
{
    public LeadInfo ToInfo() => new(FullName ?? "", Phone ?? "", Email, Source, new BranchId(BranchId), Interest, Note,
        CampaignId is { } c ? new CampaignId(c) : null, NextFollowUpAt);
}
