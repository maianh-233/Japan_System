using JPCenter.SharedKernel.Primitives;

namespace JPCenter.Modules.Finance.Domain.Pricing;

public static class PricingErrors
{
    /// <summary>TC-01.E1 — dùng chung mã lỗi số tiền của hóa đơn</summary>
    public static readonly Error InvalidFee = Error.Validation("finance.invoice.invalid_amount", "Học phí phải lớn hơn 0");

    public static readonly Error DiscountNotFound = Error.NotFound("finance.discount.not_found", "Không tìm thấy ưu đãi");
    public static readonly Error InvalidCode = Error.Validation("finance.discount.invalid_code", "Mã ưu đãi 3–15 ký tự chữ / số");
    public static readonly Error NameRequired = Error.Validation("finance.discount.name_required", "Nhập tên ưu đãi");
    /// <summary>TC-03.E1</summary>
    public static readonly Error InvalidPercent = Error.Validation("finance.discount.invalid_percent", "Giảm từ 1% đến tối đa 50%");
    public static readonly Error InvalidValue = Error.Validation("finance.discount.invalid_value", "Số tiền giảm phải lớn hơn 0 và không có số lẻ");
    /// <summary>TC-03.E2</summary>
    public static readonly Error ComboCourses = Error.Validation("finance.discount.combo_courses", "Combo cần ít nhất 2 khóa");
    /// <summary>TC-03.E3</summary>
    public static readonly Error InvalidDates = Error.Validation("finance.discount.invalid_dates", "Ngày kết thúc phải từ ngày bắt đầu trở đi");
    public static readonly Error InvalidLimit = Error.Validation("finance.discount.invalid_limit", "Giới hạn lượt phải từ 1 trở lên");
    /// <summary>TC-03.E4</summary>
    public static readonly Error LimitReached = Error.Conflict("finance.discount.limit_reached", "Ưu đãi đã hết lượt");
    /// <summary>TC-03.E5</summary>
    public static readonly Error NotAvailable = Error.Conflict("finance.discount.not_available", "Ưu đãi không áp dụng được cho ghi danh này");
    public static readonly Error LimitBelowUsed = Error.Validation("finance.discount.limit_below_used", "Giới hạn lượt không được nhỏ hơn số lượt đã dùng");

    public static readonly Error PlanNameRequired = Error.Validation("finance.installment.name_required", "Nhập tên chính sách");
    /// <summary>TC-02.E2</summary>
    public static readonly Error PlanParts = Error.Validation("finance.installment.parts", "Cần ít nhất 2 đợt, mỗi đợt > 0%");

    /// <summary>TC-02.E1</summary>
    public static Error PlanTotal(decimal total) =>
        Error.Validation("finance.installment.total", $"Tổng các đợt phải bằng 100% (hiện {total:0.##}%)");

    public static readonly Error InvalidSurcharge = Error.Validation("finance.installment.surcharge", "Phụ thu từ 0% đến 50%");
    public static readonly Error PlanNotApplicable = Error.Conflict("finance.installment.not_applicable", "Chính sách đóng đợt không áp dụng cho học phí này");
}
