namespace JPCenter.Modules.Crm.Contracts.Dtos.Reports;

/// <summary>Chiến dịch + số lead / chốt gắn với nó (mọi lúc, không tính lead ẩn) — BC-03 bước 6.</summary>
public sealed record CampaignFact(Guid Id, string Code, string Name, decimal Spent, IReadOnlyList<Guid> BranchIds, int Leads, int Won);
