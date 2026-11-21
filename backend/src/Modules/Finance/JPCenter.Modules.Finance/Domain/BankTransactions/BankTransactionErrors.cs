using JPCenter.SharedKernel.Primitives;

namespace JPCenter.Modules.Finance.Domain.BankTransactions;

public static class BankTransactionErrors
{
    public static readonly Error NotFound = Error.NotFound("finance.bank_txn.not_found", "Không tìm thấy giao dịch");
    public static readonly Error RefRequired = Error.Validation("finance.bank_txn.ref_required", "Thiếu mã giao dịch ngân hàng");
    public static readonly Error InvalidAmount = Error.Validation("finance.bank_txn.invalid_amount", "Số tiền giao dịch phải lớn hơn 0");
    /// <summary>TC-10.E2</summary>
    public static readonly Error AlreadyMatched = Error.Conflict("finance.bank_txn.already_matched", "Giao dịch đã được khớp");
    public static readonly Error NotMatched = Error.Conflict("finance.bank_txn.not_matched", "Giao dịch chưa khớp hóa đơn nào");
    public static readonly Error InvoicesRequired = Error.Validation("finance.bank_txn.invoices_required", "Chọn hóa đơn để khớp");
    public static readonly Error NoteRequired = Error.Validation("finance.bank_txn.note_required", "Nhập ghi chú lý do bỏ qua");
    /// <summary>Tách vào nhiều hóa đơn: tổng các khoản phải bằng số tiền giao dịch</summary>
    public static readonly Error AllocationMismatch =
        Error.Validation("finance.bank_txn.allocation_mismatch", "Tổng số tiền khớp vào các hóa đơn phải bằng số tiền giao dịch");
}
