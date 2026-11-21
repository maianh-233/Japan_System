using JPCenter.SharedKernel.Primitives;

namespace JPCenter.Modules.Finance.Domain.Pricing;

/// <summary>Ô nhập của ưu đãi (TC-03 bước 1)</summary>
public sealed record DiscountDetails(string? Code, string? Name, DiscountKind Kind, decimal Value, IReadOnlyList<Guid> CourseIds,
    IReadOnlyList<BranchId> BranchIds, DateOnly From, DateOnly To, int? Limit, string? Note);
