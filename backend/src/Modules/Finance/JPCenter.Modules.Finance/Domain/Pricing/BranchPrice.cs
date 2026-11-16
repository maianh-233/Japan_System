using JPCenter.SharedKernel.Domain;
using JPCenter.SharedKernel.Primitives;

namespace JPCenter.Modules.Finance.Domain.Pricing;

/// <summary>Giá riêng của khóa tại chi nhánh (không có = giá niêm yết của khóa). Bảng: finance.branch_prices, UQ (course_id, branch_id)</summary>
public sealed class BranchPrice : AggregateRoot<BranchPriceId>, IBranchScoped, IAuditable
{
    private BranchPrice() { }

    /// <summary>Academic.Course</summary>
    public Guid CourseId { get; private set; }
    public BranchId BranchId { get; private set; }
    public Money Fee { get; private set; }

    public static BranchPrice Create(Guid courseId, BranchId branchId, Money fee) => new() { Id = BranchPriceId.New(), CourseId = courseId, BranchId = branchId, Fee = fee };

    public void ChangeFee(Money fee) => Fee = fee;
}
