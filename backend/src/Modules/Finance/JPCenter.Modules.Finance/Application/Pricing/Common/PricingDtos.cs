using JPCenter.Modules.Finance.Domain.Pricing;

namespace JPCenter.Modules.Finance.Application.Pricing.Common;

/// <summary>Giá riêng của khóa tại chi nhánh — khớp <c>BranchPrice</c> ở <c>frontend/src/features/finance/types.ts</c></summary>
public sealed record BranchPriceDto(Guid CourseId, Guid BranchId, decimal Fee);

/// <summary>Ưu đãi — khớp <c>Promotion</c> ở frontend (kind: percent · amount · combo)</summary>
public sealed record DiscountDto(Guid Id, string Code, string Name, DiscountKind Kind, decimal Value, IReadOnlyList<Guid> CourseIds,
    IReadOnlyList<Guid> BranchIds, DateOnly From, DateOnly To, int? Limit, int Used, bool Active, string? Note, uint RowVersion);

/// <summary>Chính sách đóng nhiều đợt — khớp <c>InstallmentPlan</c> ở frontend</summary>
public sealed record InstallmentPlanDto(Guid Id, string Name, IReadOnlyList<InstallmentPart> Parts, decimal MinFee, decimal Surcharge, bool Active,
    uint RowVersion);

public static class PricingMapping
{
    public static DiscountDto ToDto(this Discount d, uint rowVersion) =>
        new(d.Id.Value, d.Code, d.Name, d.Kind, d.Value, d.CourseIds, d.BranchIds.Select(b => b.Value).ToList(), d.From, d.To, d.Limit, d.Used,
            d.Active, d.Note, rowVersion);

    public static InstallmentPlanDto ToDto(this InstallmentPlan p, uint rowVersion) =>
        new(p.Id.Value, p.Name, p.Parts, p.MinFee.Amount, p.Surcharge, p.Active, rowVersion);
}
