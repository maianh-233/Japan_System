using JPCenter.SharedKernel.Primitives;

namespace JPCenter.Modules.Finance.Domain.BankTransactions;

/// <summary>Id giao dịch ngân hàng</summary>
public readonly record struct BankTransactionId(Guid Value)
{
    public static BankTransactionId New() => new(IdGenerator.NewId());
}
