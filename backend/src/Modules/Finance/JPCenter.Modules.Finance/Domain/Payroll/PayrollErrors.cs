using JPCenter.SharedKernel.Primitives;

namespace JPCenter.Modules.Finance.Domain.Payroll;

public static class PayrollErrors
{
    public static readonly Error InvalidMonth = Error.Validation("finance.payroll.invalid_month", "Tháng lương phải dạng YYYY-MM");
    /// <summary>TC-13.E2 — tháng đã chốt: phải mở khóa trước khi sửa điều chỉnh / xác nhận buổi</summary>
    public static readonly Error Locked = Error.Conflict("finance.payroll.locked", "Bảng lương tháng này đã chốt — mở khóa trước khi sửa");
    /// <summary>TC-13.E3 — đã chi lương thì không mở khóa / chi lại</summary>
    public static readonly Error AlreadyPaid = Error.Conflict("finance.payroll.already_paid", "Bảng lương đã chi — không mở khóa hay chi lại được");
    public static readonly Error NotLocked = Error.Conflict("finance.payroll.not_locked", "Bảng lương chưa chốt");
    public static readonly Error NotFound = Error.NotFound("finance.payroll.not_found", "Không tìm thấy bảng lương đã chốt");
    /// <summary>TC-13.E1</summary>
    public static readonly Error InvalidAdjustAmount =
        Error.Validation("finance.pay_adjust.invalid_amount", "Số tiền phải khác 0 và không có số lẻ");
    /// <summary>TC-13.E1</summary>
    public static readonly Error AdjustReasonRequired = Error.Validation("finance.pay_adjust.reason_required", "Cần ghi lý do");
}
