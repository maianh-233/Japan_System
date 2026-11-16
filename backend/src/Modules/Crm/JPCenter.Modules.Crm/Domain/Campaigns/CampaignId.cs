using JPCenter.SharedKernel.Primitives;

namespace JPCenter.Modules.Crm.Domain.Campaigns;

/// <summary>Id chiến dịch</summary>
public readonly record struct CampaignId(Guid Value)
{
    public static CampaignId New() => new(IdGenerator.NewId());
}
