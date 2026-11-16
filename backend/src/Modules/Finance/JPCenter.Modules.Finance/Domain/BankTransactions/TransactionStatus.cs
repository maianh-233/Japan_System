namespace JPCenter.Modules.Finance.Domain.BankTransactions;

/// <summary>Trạng thái đối soát</summary>
public enum TransactionStatus
{
    /// <summary>Chưa khớp</summary>
    Unmatched,
    /// <summary>Tự khớp</summary>
    Auto,
    /// <summary>Khớp tay</summary>
    Matched,
    /// <summary>Bỏ qua</summary>
    Ignored,
}
