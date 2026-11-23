using JPCenter.SharedKernel.Primitives;

namespace JPCenter.Modules.Work.Domain.Shifts;

/// <summary>Lỗi xếp ca (user story 14, XC-xx) — mã trùng tài liệu.</summary>
public static class ShiftErrors
{
    public static readonly Error NotFound = Error.NotFound("work.shift.not_found", "Không tìm thấy ca");
    /// <summary>XC-01.E1, XC-04</summary>
    public static readonly Error PastDate = Error.Validation("work.shift.past_date", "Không sửa ca của ngày đã qua — sửa công ở Chấm công");
    /// <summary>XC-01.E2</summary>
    public static readonly Error Busy = Error.Conflict("work.shift.busy", "Nhân viên đã báo bận ca này — chọn người khác hoặc hỏi lại nhân viên");
    /// <summary>XC-01.E3</summary>
    public static readonly Error EmployeeInvalid = Error.Validation("work.shift.employee_invalid", "Nhân viên không làm theo ca / đã nghỉ / khác chi nhánh");
    /// <summary>XC-01.E6</summary>
    public static readonly Error SwapPending = Error.Conflict("work.shift.swap_pending", "Ca đang có đơn đổi ca chờ duyệt");
    /// <summary>XC-04</summary>
    public static readonly Error NotShiftBased = Error.Forbidden("work.shift.not_shift_based", "Phần đăng ký lịch rảnh / bận dành cho nhân sự làm theo ca");
}
