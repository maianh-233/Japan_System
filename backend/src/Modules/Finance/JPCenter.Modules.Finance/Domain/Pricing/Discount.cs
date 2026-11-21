using System.Text.RegularExpressions;
using JPCenter.SharedKernel.Domain;
using JPCenter.SharedKernel.Primitives;

namespace JPCenter.Modules.Finance.Domain.Pricing;

/// <summary>
/// Ưu đãi học phí (TC-03): giảm %, giảm số tiền, combo nhiều khóa; có thời gian, chi nhánh và giới hạn lượt.
/// (Frontend gọi là "Promotion" — đổi tên vì Promotion = lên lớp.) Bảng: finance.discounts
/// </summary>
public sealed partial class Discount : AggregateRoot<DiscountId>, IAuditable, ISoftDeletable
{
    public const decimal MaxPercent = 50;
    /// <summary>Giảm % làm tròn tới 1.000đ</summary>
    public const decimal RoundTo = 1000;

    private Discount() { }

    /// <summary>Mã (chữ hoa) — UQ</summary>
    public string Code { get; private set; } = null!;
    public string Name { get; private set; } = null!;
    public DiscountKind Kind { get; private set; }
    /// <summary>Percent: số %; Amount / Combo: số tiền giảm (đồng)</summary>
    public decimal Value { get; private set; }
    /// <summary>Combo: các khóa phải đăng ký cùng (Academic.Course); loại khác: rỗng = mọi khóa, có = chỉ các khóa này</summary>
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

    public static Result<Discount> Create(DiscountDetails details)
    {
        var d = new Discount { Id = DiscountId.New(), Active = true };
        var set = d.Update(details);
        return set.IsFailure ? set.Error : d;
    }

    /// <summary>Sửa ưu đãi — kiểm như lúc tạo; giới hạn lượt không nhỏ hơn số đã dùng</summary>
    public Result Update(DiscountDetails x)
    {
        var code = x.Code?.Trim().ToUpperInvariant() ?? "";
        if (!CodeFormat().IsMatch(code)) return PricingErrors.InvalidCode;
        if (string.IsNullOrWhiteSpace(x.Name)) return PricingErrors.NameRequired;
        if (x.Kind == DiscountKind.Percent && x.Value is <= 0 or > MaxPercent) return PricingErrors.InvalidPercent;
        if (x.Kind != DiscountKind.Percent && (x.Value <= 0 || decimal.Truncate(x.Value) != x.Value)) return PricingErrors.InvalidValue;
        var courses = x.CourseIds.Distinct().ToList();
        if (x.Kind == DiscountKind.Combo && courses.Count < 2) return PricingErrors.ComboCourses;
        if (x.To < x.From) return PricingErrors.InvalidDates;
        if (x.Limit is < 1) return PricingErrors.InvalidLimit;
        if (x.Limit is { } limit && limit < Used) return PricingErrors.LimitBelowUsed;
        (Code, Name, Kind, Value, CourseIds, BranchIds) = (code, x.Name.Trim(), x.Kind, x.Value, courses, x.BranchIds.Distinct().ToList());
        (From, To, Limit, Note) = (x.From, x.To, x.Limit, string.IsNullOrWhiteSpace(x.Note) ? null : x.Note.Trim());
        return Result.Success();
    }

    public void SetActive(bool active) => Active = active;

    /// <summary>
    /// TC-03.E5 — dùng được cho ghi danh khóa <paramref name="courseId"/> ở chi nhánh <paramref name="branchId"/> ngày <paramref name="today"/>?
    /// Combo: khóa này thuộc combo VÀ HV có (hoặc đang ghi danh cùng) đủ các khóa còn lại (<paramref name="otherCourseIds"/>).
    /// </summary>
    public Result CheckApplicable(Guid courseId, BranchId branchId, DateOnly today, IReadOnlyCollection<Guid> otherCourseIds)
    {
        if (!Active || today < From || today > To) return PricingErrors.NotAvailable;
        if (BranchIds.Count > 0 && !BranchIds.Contains(branchId)) return PricingErrors.NotAvailable;
        if (Kind == DiscountKind.Combo)
        {
            if (!CourseIds.Contains(courseId) || CourseIds.Where(c => c != courseId).Any(c => !otherCourseIds.Contains(c))) return PricingErrors.NotAvailable;
        }
        else if (CourseIds.Count > 0 && !CourseIds.Contains(courseId)) return PricingErrors.NotAvailable;
        if (Limit is { } l && Used >= l) return PricingErrors.LimitReached;
        return Result.Success();
    }

    /// <summary>Số tiền giảm cho học phí <paramref name="fee"/>: % làm tròn 1.000đ; số tiền / combo không vượt học phí</summary>
    public Money Apply(Money fee)
    {
        var off = Kind == DiscountKind.Percent
            ? decimal.Round(fee.Amount * Value / 100m / RoundTo, 0, MidpointRounding.AwayFromZero) * RoundTo
            : Value;
        return Money.FromTrusted(Math.Min(off, fee.Amount));
    }

    /// <summary>Dùng 1 lượt (cùng transaction với hóa đơn; khóa dòng ở Infrastructure để 2 người không cùng lấy lượt cuối — E4)</summary>
    public Result Use()
    {
        if (Limit is { } l && Used >= l) return PricingErrors.LimitReached;
        Used++;
        return Result.Success();
    }

    /// <summary>TC-03.E7 — hóa đơn dùng mã bị hủy → trả lại 1 lượt</summary>
    public void Release()
    {
        if (Used > 0) Used--;
    }

    [GeneratedRegex("^[A-Z0-9]{3,15}$")]
    private static partial Regex CodeFormat();
}
