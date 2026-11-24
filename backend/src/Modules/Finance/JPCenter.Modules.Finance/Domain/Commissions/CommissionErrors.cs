using JPCenter.SharedKernel.Primitives;

namespace JPCenter.Modules.Finance.Domain.Commissions;

public static class CommissionErrors
{
    /// <summary>TC-12.E2</summary>
    public static readonly Error InvalidPercent = Error.Validation("finance.commission.invalid_percent", "Phần trăm từ 0 đến 100");
    /// <summary>TC-12.E1</summary>
    public static readonly Error ZeroTierRequired = Error.Validation("finance.commission.zero_tier_required", "Cần có bậc bắt đầu từ 0đ");
    /// <summary>TC-12.E1</summary>
    public static readonly Error DuplicateTier = Error.Validation("finance.commission.duplicate_tier", "Các mốc không được trùng nhau");
    public static readonly Error InvalidTarget = Error.Validation("finance.commission.invalid_target", "Chỉ tiêu không được âm, không có số lẻ");
    public static readonly Error SaleRequired = Error.Validation("finance.commission.sale_required", "Chọn Sale được ghi nhận");
}
