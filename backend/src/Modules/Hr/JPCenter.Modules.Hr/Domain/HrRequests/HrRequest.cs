using JPCenter.SharedKernel.Domain;
using JPCenter.SharedKernel.Primitives;
using JPCenter.Modules.Hr.Domain.Employees;

namespace JPCenter.Modules.Hr.Domain.HrRequests;

/// <summary>
/// Đơn từ nhân viên: nghỉ phép, đổi ca, công tác, muộn / sớm, đề xuất, tạm ứng — duyệt nhiều cấp theo thứ tự.
/// Bảng: hr.hr_requests (+ hr_request_steps)
/// </summary>
public sealed class HrRequest : AggregateRoot<HrRequestId>, IBranchScoped, IAuditable
{
    private readonly List<ApprovalStep> _steps = [];

    private HrRequest() { }

    public string Code { get; private set; } = null!;
    public HrRequestType Type { get; private set; }
    public EmployeeId EmployeeId { get; private set; }
    public BranchId BranchId { get; private set; }
    public DateTimeOffset From { get; private set; }
    public DateTimeOffset To { get; private set; }
    /// <summary>Số ngày nghỉ (nghỉ phép)</summary>
    public decimal? Days { get; private set; }
    /// <summary>Nghỉ có lương / không lương</summary>
    public bool? PaidLeave { get; private set; }
    /// <summary>Đổi ca: ca của mình ↔ ca của đồng nghiệp (Work.ShiftAssignment)</summary>
    public Guid? SwapMyShiftId { get; private set; }
    public EmployeeId? SwapWithEmployeeId { get; private set; }
    public Guid? SwapTheirShiftId { get; private set; }
    /// <summary>Đề xuất / tạm ứng</summary>
    public Money? Amount { get; private set; }
    public string? Title { get; private set; }
    public string Reason { get; private set; } = null!;
    public HrRequestStatus Status { get; private set; }
    public DateTimeOffset RequestedAt { get; private set; }
    public IReadOnlyList<ApprovalStep> Steps => _steps;

    public static Result<HrRequest> Create(string code, HrRequestType type, EmployeeId employeeId, BranchId branchId, DateTimeOffset from,
        DateTimeOffset to, string reason, IEnumerable<(string Role, Guid ApproverId)> approvers, DateTimeOffset at)
    {
        if (to < from) return Error.Validation("hr.request.invalid_dates", "Thời gian kết thúc phải sau bắt đầu");
        var r = new HrRequest
        {
            Id = HrRequestId.New(), Code = code, Type = type, EmployeeId = employeeId, BranchId = branchId, From = from, To = to, Reason = reason,
            RequestedAt = at, Status = HrRequestStatus.Pending,
        };
        var order = 1;
        foreach (var (role, approverId) in approvers) r._steps.Add(new ApprovalStep(order++, role, approverId));
        if (r._steps.Count == 0) return Error.Validation("hr.request.no_approver", "Đơn cần ít nhất 1 người duyệt");
        return r;
    }

    public void SetLeave(decimal days, bool paid) => (Days, PaidLeave) = (days, paid);
    public void SetSwap(Guid myShiftId, EmployeeId withEmployeeId, Guid theirShiftId) => (SwapMyShiftId, SwapWithEmployeeId, SwapTheirShiftId) = (myShiftId, withEmployeeId, theirShiftId);
    public void SetAmount(Money amount, string? title) => (Amount, Title) = (amount, title);

    /// <summary>Người duyệt của bước hiện tại quyết định. Từ chối ở bất kỳ bước nào → đơn bị từ chối.</summary>
    public Result Decide(Guid approverId, ApprovalDecision decision, string? comment, DateTimeOffset at)
    {
        if (Status != HrRequestStatus.Pending) return Error.Conflict("hr.request.not_pending", "Đơn đã được xử lý");
        var step = _steps.OrderBy(s => s.Order).FirstOrDefault(s => s.Decision is null);
        if (step is null || step.ApproverId != approverId) return Error.Forbidden("hr.request.not_your_turn", "Chưa đến lượt bạn duyệt đơn này");
        step.Decide(decision, comment, at);
        if (decision == ApprovalDecision.Rejected) Status = HrRequestStatus.Rejected;
        else if (_steps.All(s => s.Decision == ApprovalDecision.Approved)) Status = HrRequestStatus.Approved;
        return Result.Success();
    }

    public Result Cancel(EmployeeId by)
    {
        if (by != EmployeeId) return Error.Forbidden("hr.request.only_owner", "Chỉ người gửi được hủy đơn");
        if (Status != HrRequestStatus.Pending) return Error.Conflict("hr.request.not_pending", "Đơn đã được xử lý");
        Status = HrRequestStatus.Cancelled;
        return Result.Success();
    }
}
