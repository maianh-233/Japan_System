namespace JPCenter.Modules.Finance.Application.Payroll.Common;

/// <summary>Thưởng / phạt / phụ cấp tháng — khớp <c>PayAdjust</c> frontend (<c>by</c> = tên người nhập)</summary>
public sealed record PayAdjustDto(Guid Id, string Month, Guid EmployeeId, decimal Amount, string Reason, string By);

/// <summary>1 buổi của GV trên bảng lương (TC-13) — khớp <c>TeachItem</c> frontend. Buổi chưa xác nhận có <c>amount</c> nhưng chưa cộng vào lương.</summary>
/// <param name="BranchId">Chi nhánh của lớp — buổi tính lương ở chi nhánh này (TC-13.E5)</param>
/// <param name="Role">"teacher" · "substitute" · "assistant"</param>
/// <param name="Rate">Đơn giá / giờ áp dụng (offline / online theo lớp)</param>
/// <param name="Bonus">Thưởng dạy thay</param>
public sealed record TeachItemDto(
    Guid SessionId,
    Guid ClassId,
    string ClassCode,
    string ClassName,
    Guid BranchId,
    DateOnly Date,
    string StartTime,
    string EndTime,
    string Role,
    bool Online,
    string? CheckIn,
    string? CheckOut,
    int Minutes,
    decimal Rate,
    decimal Amount,
    bool Confirmed,
    bool Late,
    decimal Bonus);

/// <summary>
/// Phiếu lương GV / trợ giảng của 1 chi nhánh trong tháng (TC-13) — khớp <c>TeacherPay</c> frontend (nhân viên rút gọn: id, mã, tên).
/// Buổi tính vào chi nhánh của LỚP (E5); lương cứng tính ở chi nhánh chính của GV.
/// </summary>
/// <param name="Items">Mọi buổi đã dạy (đã + chờ xác nhận)</param>
/// <param name="Cancelled">Số buổi bị hủy của GV trong tháng (không tính)</param>
/// <param name="Hours">Giờ dạy đã xác nhận</param>
/// <param name="Total">Thực nhận = lương cứng + tiền dạy + thưởng dạy thay − phạt muộn ± điều chỉnh (âm → 0)</param>
public sealed record TeacherPayDto(
    Guid EmployeeId,
    string Code,
    string FullName,
    string Kind,
    Guid BranchId,
    IReadOnlyList<TeachItemDto> Items,
    int ConfirmedCount,
    int PendingCount,
    int Cancelled,
    decimal Hours,
    decimal Base,
    decimal Teaching,
    decimal SubstituteBonus,
    decimal LateFine,
    decimal Adjust,
    IReadOnlyList<PayAdjustDto> Adjusts,
    decimal Total);

/// <summary>Phiếu lương NV (TC-14) — khớp <c>StaffPay</c> frontend</summary>
/// <param name="HasTimesheet">false = chưa có chấm công, tạm lấy công chuẩn (E2 — nhãn "Chưa có chấm công")</param>
/// <param name="PayDays">Công tính lương (≤ công chuẩn)</param>
/// <param name="Gross">Tổng thu nhập = lương theo công + phụ cấp + hoa hồng ± điều chỉnh</param>
/// <param name="Net">Thực nhận = tổng − phạt − BHXH − thuế TNCN (âm → 0)</param>
public sealed record StaffPayDto(
    Guid EmployeeId,
    string Code,
    string FullName,
    string Department,
    Guid BranchId,
    int Worked,
    int PaidLeave,
    int UnpaidLeave,
    int Absent,
    int Late,
    int NoOut,
    bool HasTimesheet,
    int PayDays,
    int StandardDays,
    decimal Base,
    decimal BasePay,
    decimal Allowance,
    decimal Commission,
    decimal Adjust,
    IReadOnlyList<PayAdjustDto> Adjusts,
    decimal Gross,
    decimal Fines,
    decimal Insurance,
    decimal Tax,
    decimal Net);

/// <summary>Bảng lương đang khóa của 1 chi nhánh — khớp <c>PayrollLock</c> frontend (<c>lockedBy</c> = tên). Mở khóa thì không còn trong danh sách.</summary>
/// <param name="Kind">"teacher" · "staff"</param>
public sealed record PayrollLockDto(string Kind, string Month, Guid BranchId, string LockedBy, DateTimeOffset LockedAt, decimal Total,
    DateTimeOffset? PaidAt);

/// <summary>Bảng lương tháng (TC-13 / 14): các dòng, khóa theo chi nhánh, hạn chốt (Cài đặt <c>payroll.lockDay</c>)</summary>
/// <param name="Branches">Chi nhánh trong phạm vi xem</param>
/// <param name="Locked">Mọi chi nhánh trong phạm vi đã chốt — số liệu lấy từ bản chốt</param>
/// <param name="LockOverdue">Quá hạn chốt mà chưa chốt hết (nhãn cảnh báo)</param>
/// <param name="PendingSessions">Lương GV: số buổi chờ xác nhận (cảnh báo khi chốt)</param>
public sealed record PayrollBoardDto<TRow>(
    string Month,
    IReadOnlyList<Guid> Branches,
    IReadOnlyList<TRow> Rows,
    IReadOnlyList<PayrollLockDto> Locks,
    bool Locked,
    DateOnly LockDeadline,
    bool LockOverdue,
    int PendingSessions,
    decimal Total);
