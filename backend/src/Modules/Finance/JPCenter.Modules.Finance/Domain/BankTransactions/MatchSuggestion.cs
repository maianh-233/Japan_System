using JPCenter.Modules.Finance.Domain.Invoices;

namespace JPCenter.Modules.Finance.Domain.BankTransactions;

/// <summary>Gợi ý khớp: hóa đơn + điểm + lý do (hiển thị cho kế toán)</summary>
public sealed record MatchSuggestion(InvoiceId InvoiceId, int Score, IReadOnlyList<string> Reasons);
