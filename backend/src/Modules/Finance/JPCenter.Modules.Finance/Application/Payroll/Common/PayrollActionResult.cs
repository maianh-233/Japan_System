namespace JPCenter.Modules.Finance.Application.Payroll.Common;

/// <summary>Kết quả chốt / mở khóa / đã chi: các bảng lương bị tác động + cảnh báo (vd còn buổi chờ xác nhận)</summary>
public sealed record PayrollActionResult(IReadOnlyList<PayrollLockDto> Locks, IReadOnlyList<string> Warnings);
