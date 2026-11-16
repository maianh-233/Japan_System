using JPCenter.SharedKernel.Primitives;

namespace JPCenter.Modules.Academic.Domain.Promotions;

/// <summary>Id đề xuất lên lớp</summary>
public readonly record struct PromotionCandidateId(Guid Value)
{
    public static PromotionCandidateId New() => new(IdGenerator.NewId());
}
