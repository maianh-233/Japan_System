using JPCenter.SharedKernel.Primitives;
using JPCenter.Modules.Hr.Domain.Employees;

namespace JPCenter.Modules.Hr.Domain.HrRequests;

/// <summary>Nội dung riêng theo loại đơn (bỏ trống phần không dùng)</summary>
/// <param name="PaidLeave">Nghỉ phép có lương (trừ phép năm)</param>
/// <param name="SwapMyShiftId">Đổi ca: ca của người gửi (Work.ShiftAssignment)</param>
/// <param name="SwapTheirShiftId">Đổi ca: ca của đồng nghiệp (Work.ShiftAssignment)</param>
public sealed record HrRequestDetails(
    bool? PaidLeave = null,
    Money? Amount = null,
    string? Title = null,
    Guid? SwapMyShiftId = null,
    EmployeeId? SwapWithEmployeeId = null,
    Guid? SwapTheirShiftId = null);
