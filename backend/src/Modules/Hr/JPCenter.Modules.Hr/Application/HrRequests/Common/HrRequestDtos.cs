using JPCenter.Modules.Hr.Domain.HrRequests;

namespace JPCenter.Modules.Hr.Application.HrRequests.Common;

/// <summary>
/// 1 bước duyệt — khớp <c>ApprovalStep</c> frontend: <c>approver</c> = tên (người quyết thật nếu đã quyết, kèm "(duyệt thay)"),
/// thêm <c>approverId</c> (null = bước mở, người có <c>hr.requests.approve</c> duyệt).
/// </summary>
public sealed record ApprovalStepDto(int Order, string Role, string Approver, Guid? ApproverId, ApprovalDecision? Decision, DateTimeOffset? At,
    string? Comment, bool OnBehalf, Guid? DecidedById);

/// <summary>Đơn đổi ca: ca của tôi ↔ ca của đồng nghiệp (Work.ShiftAssignment)</summary>
public sealed record SwapDto(Guid MyShiftId, Guid WithEmployeeId, Guid TheirShiftId, string? WithEmployeeName);

/// <summary>Đơn từ — khớp <c>HrRequest</c> frontend; thêm tên người gửi, bước hiện tại và quyền của người đang xem.</summary>
public sealed record HrRequestDto(Guid Id, string Code, HrRequestType Type, Guid EmployeeId, string EmployeeName, Guid BranchId, DateOnly From,
    DateOnly To, decimal? Days, bool? PaidLeave, SwapDto? Swap, decimal? Amount, string? Title, string Reason, HrRequestStatus Status,
    IReadOnlyList<ApprovalStepDto> Steps, DateTimeOffset CreatedAt, int? CurrentStep, bool CanDecide, bool CanCancel);

/// <summary>DN-04 — chờ tôi duyệt, nghỉ phép tuần này (đã duyệt), tổng tiền đang chờ (đề xuất + tạm ứng)</summary>
public sealed record HrRequestStatsDto(int Todo, int LeaveThisWeek, decimal PendingAmount);

/// <summary>Danh sách đơn có phân trang + thống kê</summary>
public sealed record HrRequestListDto(IReadOnlyList<HrRequestDto> Items, int Total, int Page, int PageSize, HrRequestStatsDto Stats);
