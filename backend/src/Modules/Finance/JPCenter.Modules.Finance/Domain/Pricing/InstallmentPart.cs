namespace JPCenter.Modules.Finance.Domain.Pricing;

/// <summary>1 đợt đóng: % học phí, hạn = ngày khai giảng + OffsetDays</summary>
public sealed record InstallmentPart(decimal Percent, int OffsetDays);
