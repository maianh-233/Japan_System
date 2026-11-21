using JPCenter.SharedKernel.Domain;
using JPCenter.SharedKernel.Primitives;

namespace JPCenter.Modules.Finance.Domain.Pricing;

/// <summary>
/// Giá riêng của khóa tại chi nhánh (TC-01) — không có = giá niêm yết của khóa. Đổi giá chỉ áp cho lớp mở mới (E2, học phí lớp chốt lúc mở).
/// Bảng: finance.branch_prices, UQ (course_id, branch_id)
/// </summary>
public sealed class BranchPrice : AggregateRoot<BranchPriceId>, IBranchScoped, IAuditable
{
    private BranchPrice() { }

    /// <summary>Academic.Course</summary>
    public Guid CourseId { get; private set; }
    public BranchId BranchId { get; private set; }
    public Money Fee { get; private set; }

    public static Result<BranchPrice> Create(Guid courseId, BranchId branchId, Money fee) =>
        fee <= Money.Zero ? PricingErrors.InvalidFee : new BranchPrice { Id = BranchPriceId.New(), CourseId = courseId, BranchId = branchId, Fee = fee };

    public Result ChangeFee(Money fee)
    {
        if (fee <= Money.Zero) return PricingErrors.InvalidFee;
        Fee = fee;
        return Result.Success();
    }
}
