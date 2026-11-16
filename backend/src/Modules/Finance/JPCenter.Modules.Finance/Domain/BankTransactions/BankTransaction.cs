using JPCenter.SharedKernel.Domain;
using JPCenter.SharedKernel.Primitives;
using JPCenter.Modules.Finance.Domain.Invoices;

namespace JPCenter.Modules.Finance.Domain.BankTransactions;

/// <summary>
/// Giao dịch ngân hàng nhận qua webhook (VietQR) → tự khớp hóa đơn theo nội dung chuyển khoản; chưa khớp thì xử lý tay.
/// Bảng: finance.bank_transactions, UQ (ref)
/// </summary>
public sealed class BankTransaction : AggregateRoot<BankTransactionId>, IBranchScoped, IAuditable
{
    private BankTransaction() { }

    /// <summary>Mã giao dịch ngân hàng</summary>
    public string Ref { get; private set; } = null!;
    public DateTimeOffset At { get; private set; }
    public Money Amount { get; private set; }
    /// <summary>Nội dung chuyển khoản</summary>
    public string Content { get; private set; } = null!;
    public string Payer { get; private set; } = null!;
    /// <summary>Tài khoản nhận</summary>
    public string Account { get; private set; } = null!;
    public BranchId BranchId { get; private set; }
    public TransactionStatus Status { get; private set; }
    public InvoiceId? InvoiceId { get; private set; }
    /// <summary>null = hệ thống tự khớp</summary>
    public Guid? MatchedBy { get; private set; }
    public DateTimeOffset? MatchedAt { get; private set; }
    public string? Note { get; private set; }

    public static BankTransaction Receive(string @ref, DateTimeOffset at, Money amount, string content, string payer, string account, BranchId branchId) =>
        new() { Id = BankTransactionId.New(), Ref = @ref, At = at, Amount = amount, Content = content, Payer = payer, Account = account, BranchId = branchId, Status = TransactionStatus.Unmatched };

    public Result Match(InvoiceId invoiceId, Guid? by, DateTimeOffset at)
    {
        if (Status is TransactionStatus.Matched or TransactionStatus.Auto) return Error.Conflict("finance.bank_txn.already_matched", "Giao dịch đã được khớp");
        (InvoiceId, MatchedBy, MatchedAt, Status) = (invoiceId, by, at, by is null ? TransactionStatus.Auto : TransactionStatus.Matched);
        return Result.Success();
    }

    public void Ignore(string note) => (Status, Note) = (TransactionStatus.Ignored, note);
}
