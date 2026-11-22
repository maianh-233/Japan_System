using JPCenter.Modules.Finance.Domain.BankTransactions;

namespace JPCenter.Modules.Finance.Application.Reconciliation.Common;

/// <summary>
/// Giao dịch ngân hàng — khớp <c>BankTxn</c> ở <c>frontend/src/features/finance/types.ts</c> (status: unmatched · auto · matched · ignored;
/// <c>invoiceId</c> = hóa đơn đầu tiên) + mọi hóa đơn khi tách khớp (TC-10.E3).
/// </summary>
/// <param name="MatchedBy">Tên người khớp tay / bỏ qua; null khi tự khớp</param>
public sealed record BankTxnDto(Guid Id, string Ref, DateTimeOffset At, decimal Amount, string Content, string Payer, string Account, Guid BranchId,
    TransactionStatus Status, Guid? InvoiceId, IReadOnlyList<Guid> InvoiceIds, IReadOnlyList<string> InvoiceCodes, string? MatchedBy,
    DateTimeOffset? MatchedAt, string? Note);

/// <summary>Gợi ý khớp (TC-10 bước 3): điểm ≥ 30, tối đa 5, kèm lý do</summary>
public sealed record MatchSuggestionDto(Guid InvoiceId, string Code, string StudentName, string? ClassCode, decimal Remaining, Guid BranchId, int Score,
    IReadOnlyList<string> Reasons);
