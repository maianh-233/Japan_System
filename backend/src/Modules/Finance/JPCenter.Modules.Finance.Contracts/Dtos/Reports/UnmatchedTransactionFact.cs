namespace JPCenter.Modules.Finance.Contracts.Dtos.Reports;

/// <summary>Giao dịch ngân hàng chưa khớp hóa đơn (BC-02 Kế toán).</summary>
public sealed record UnmatchedTransactionFact(Guid Id, Guid BranchId, DateTimeOffset At, decimal Amount, string Payer, string Content);
