using JPCenter.SharedKernel.Domain;
using JPCenter.SharedKernel.Primitives;

namespace JPCenter.Modules.Finance.Domain.Pricing;

/// <summary>
/// Ưu đãi học phí: giảm %, giảm số tiền, combo nhiều khóa; có thời gian và giới hạn lượt.
/// (Frontend gọi là "Promotion" — đổi tên vì Promotion = lên lớp.) Bảng: finance.discounts
/// </summary>
public sealed class Discount : AggregateRoot<DiscountId>, IAuditable, ISoftDeletable
{
    private Discount() { }

    public string Code { get; private set; } = null!;
    public string Name { get; private set; } = null!;
    public DiscountKind Kind { get; private set; }
    /// <summary>Percent: số %; Amount / Combo: số tiền giảm (đồng)</summary>
    public decimal Value { get; private set; }
    /// <summary>Combo: các khóa phải đăng ký cùng lúc (Academic.Course)</summary>
    public List<Guid> CourseIds { get; private set; } = [];
    /// <summary>Rỗng = mọi chi nhánh</summary>
    public List<BranchId> BranchIds { get; private set; } = [];
    public DateOnly From { get; private set; }
    public DateOnly To { get; private set; }
    /// <summary>Giới hạn lượt dùng — null = không giới hạn</summary>
    public int? Limit { get; private set; }
    public int Used { get; private set; }
    public bool Active { get; private set; }
    public string? Note { get; private set; }

    public static Result<Discount> Create(string code, string name, DiscountKind kind, decimal value, List<Guid> courseIds, List<BranchId> branchIds,
        DateOnly from, DateOnly to, int? limit, string? note)
    {
        if (to < from) return Error.Validation("finance.discount.invalid_dates", "Ngày kết thúc phải sau ngày bắt đầu");
        if (kind == DiscountKind.Percent && value is <= 0 or > 100) return Error.Validation("finance.discount.invalid_percent", "Phần trăm giảm từ 1 đến 100");
        if (kind == DiscountKind.Combo && courseIds.Count < 2) return Error.Validation("finance.discount.combo_courses", "Combo cần ít nhất 2 khóa");
        return new Discount
        {
            Id = DiscountId.New(), Code = code, Name = name, Kind = kind, Value = value, CourseIds = courseIds, BranchIds = branchIds,
            From = from, To = to, Limit = limit, Note = note, Active = true,
        };
    }

    /// <summary>Số tiền giảm cho học phí cho trước</summary>
    public Money Apply(Money fee) => Kind == DiscountKind.Percent ? fee.Percent(Value) : Money.FromTrusted(Math.Min(Value, fee.Amount));

    public Result Use(DateOnly today)
    {
        if (!Active || today < From || today > To) return Error.Conflict("finance.discount.not_available", "Ưu đãi không còn hiệu lực");
        if (Limit is { } l && Used >= l) return Error.Conflict("finance.discount.limit_reached", "Ưu đãi đã hết lượt");
        Used++;
        return Result.Success();
    }

    public void SetActive(bool active) => Active = active;
}
