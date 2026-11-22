using JPCenter.SharedKernel.Domain;
using JPCenter.SharedKernel.Primitives;
using JPCenter.Modules.Finance.Domain.Invoices;

namespace JPCenter.Modules.Finance.Domain.BankTransactions;

/// <summary>
/// Giao dịch ngân hàng nhận qua webhook (TC-10) → tự khớp hóa đơn theo nội dung chuyển khoản; còn lại kế toán khớp tay / bỏ qua.
/// 1 giao dịch có thể tách vào nhiều hóa đơn của cùng HV / PH (E3) — mỗi hóa đơn 1 khoản thu mang <c>bank_transaction_id</c>.
/// Bảng: finance.bank_transactions, UQ (ref) — webhook gửi lại không tạo trùng (E1)
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
    /// <summary>Các hóa đơn đã khớp (tách được vào nhiều hóa đơn)</summary>
    public List<InvoiceId> InvoiceIds { get; private set; } = [];
    /// <summary>null = hệ thống tự khớp</summary>
    public Guid? MatchedBy { get; private set; }
    public DateTimeOffset? MatchedAt { get; private set; }
    public string? Note { get; private set; }

    public static Result<BankTransaction> Receive(string? @ref, DateTimeOffset at, Money amount, string? content, string? payer, string? account,
        BranchId branchId)
    {
        if (string.IsNullOrWhiteSpace(@ref)) return BankTransactionErrors.RefRequired;
        if (amount <= Money.Zero) return BankTransactionErrors.InvalidAmount;
        return new BankTransaction
        {
            Id = BankTransactionId.New(), Ref = @ref.Trim(), At = at, Amount = amount, Content = content?.Trim() ?? "", Payer = payer?.Trim() ?? "",
            Account = account?.Trim() ?? "", BranchId = branchId, Status = TransactionStatus.Unmatched,
        };
    }

    /// <summary>
    /// Khớp vào các hóa đơn (Application đã ghi khoản thu tương ứng, tổng = số tiền giao dịch). <paramref name="by"/> null = tự khớp.
    /// Giao dịch đã khớp → E2; đã "bỏ qua" vẫn khớp lại được (kế toán đổi ý).
    /// </summary>
    public Result Match(IReadOnlyList<InvoiceId> invoiceIds, Guid? by, DateTimeOffset at)
    {
        if (Status is TransactionStatus.Matched or TransactionStatus.Auto) return BankTransactionErrors.AlreadyMatched;
        if (invoiceIds.Count == 0) return BankTransactionErrors.InvoicesRequired;
        (InvoiceIds, MatchedBy, MatchedAt, Status) = (invoiceIds.Distinct().ToList(), by, at, by is null ? TransactionStatus.Auto : TransactionStatus.Matched);
        Note = null;
        return Result.Success();
    }

    /// <summary>Bỏ khớp (khớp nhầm) / bỏ "bỏ qua" → về "chưa khớp"; trả các hóa đơn cần xóa khoản thu tương ứng</summary>
    public Result<IReadOnlyList<InvoiceId>> Unmatch()
    {
        // Đã bỏ qua (nhầm) → đưa về chưa khớp, không có khoản thu nào để gỡ
        if (Status == TransactionStatus.Ignored)
        {
            (Status, Note) = (TransactionStatus.Unmatched, null);
            return Result.Success<IReadOnlyList<InvoiceId>>([]);
        }
        if (Status is not (TransactionStatus.Matched or TransactionStatus.Auto)) return BankTransactionErrors.NotMatched;
        var invoices = InvoiceIds.ToList();
        (InvoiceIds, MatchedBy, MatchedAt, Status) = ([], null, null, TransactionStatus.Unmatched);
        return invoices;
    }

    /// <summary>Không phải học phí (hoàn ứng, chuyển nội bộ…) — ghi chú bắt buộc</summary>
    public Result Ignore(string? note)
    {
        if (Status is TransactionStatus.Matched or TransactionStatus.Auto) return BankTransactionErrors.AlreadyMatched;
        if (string.IsNullOrWhiteSpace(note)) return BankTransactionErrors.NoteRequired;
        (Status, Note) = (TransactionStatus.Ignored, note.Trim());
        return Result.Success();
    }
}
