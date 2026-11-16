namespace JPCenter.SharedKernel.Primitives;

/// <summary>Tiền VND — số nguyên đồng, không âm khi tạo từ ngoài. Lưu DB: numeric(18,0).</summary>
public readonly record struct Money : IComparable<Money>
{
    private Money(decimal amount) => Amount = amount;

    public decimal Amount { get; }

    public static readonly Money Zero = new(0);

    public static Result<Money> Create(decimal amount)
    {
        if (amount < 0)
            return Error.Validation("money.negative", "Số tiền không được âm");
        if (decimal.Truncate(amount) != amount)
            return Error.Validation("money.fraction", "Số tiền VND không có số lẻ");
        return new Money(amount);
    }

    /// <summary>Dùng khi đọc lại từ DB / giá trị đã kiểm tra.</summary>
    public static Money FromTrusted(decimal amount) => new(amount);

    public static Money operator +(Money a, Money b) => new(a.Amount + b.Amount);
    public static Money operator -(Money a, Money b) => new(a.Amount - b.Amount);
    public static bool operator >(Money a, Money b) => a.Amount > b.Amount;
    public static bool operator <(Money a, Money b) => a.Amount < b.Amount;
    public static bool operator >=(Money a, Money b) => a.Amount >= b.Amount;
    public static bool operator <=(Money a, Money b) => a.Amount <= b.Amount;

    /// <summary>Nhân theo % (giảm giá, hoa hồng) — làm tròn về đồng.</summary>
    public Money Percent(decimal percent) => new(decimal.Round(Amount * percent / 100m, 0, MidpointRounding.AwayFromZero));

    public int CompareTo(Money other) => Amount.CompareTo(other.Amount);

    public override string ToString() => $"{Amount:N0}đ";
}
