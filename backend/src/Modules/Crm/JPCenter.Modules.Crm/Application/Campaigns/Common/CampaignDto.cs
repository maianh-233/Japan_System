using JPCenter.Modules.Crm.Domain.Campaigns;
using JPCenter.Modules.Crm.Domain.Leads;

namespace JPCenter.Modules.Crm.Application.Campaigns.Common;

/// <summary>Số liệu chiến dịch tính khi đọc — chưa có lead / chốt thì chỉ số chia là null ("—", CRM-16.E3)</summary>
public sealed record CampaignStatsDto(int Leads, int Won, decimal? TargetPct, decimal? WinRate, decimal? CostPerLead, decimal? CostPerWin);

/// <summary>Chiến dịch — khớp <c>Campaign</c> frontend (người phụ trách / khóa theo Id) + số liệu</summary>
public sealed record CampaignDto(Guid Id, string Code, string Name, LeadSource Channel, IReadOnlyList<Guid> BranchIds, DateOnly StartDate,
    DateOnly EndDate, decimal Budget, decimal Spent, int? TargetLeads, CampaignStatus Status, Guid OwnerId, string Owner, string? Utm,
    IReadOnlyList<Guid> CourseIds, string? Note, DateTimeOffset CreatedAt, CampaignStatsDto Stats, uint Version);
