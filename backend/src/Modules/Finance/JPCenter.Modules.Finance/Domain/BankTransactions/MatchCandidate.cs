using JPCenter.SharedKernel.Primitives;
using JPCenter.Modules.Finance.Domain.Invoices;

namespace JPCenter.Modules.Finance.Domain.BankTransactions;

/// <summary>Hóa đơn ứng viên để đối soát (Application dựng từ hóa đơn còn nợ + tên HV / mã lớp)</summary>
public sealed record MatchCandidate(InvoiceId InvoiceId, string Code, string StudentName, string? ClassCode, Money Remaining, BranchId BranchId);
