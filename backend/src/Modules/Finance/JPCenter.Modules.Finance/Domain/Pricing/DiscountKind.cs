namespace JPCenter.Modules.Finance.Domain.Pricing;

/// <summary>Loại ưu đãi</summary>
public enum DiscountKind
{
    /// <summary>Giảm %</summary>
    Percent,
    /// <summary>Giảm số tiền</summary>
    Amount,
    /// <summary>Combo nhiều khóa</summary>
    Combo,
}
