using JPCenter.SharedKernel.Domain;
using JPCenter.SharedKernel.Primitives;
using JPCenter.Modules.Hr.Domain.Employees;

namespace JPCenter.Modules.Hr.Domain.HrRequests;

/// <summary>
/// Đơn từ nhân viên: nghỉ phép, đổi ca, công tác, muộn / sớm, đề xuất, tạm ứng — duyệt nhiều cấp theo thứ tự (DN-01 → DN-03).
/// Áp dụng khi duyệt xong (trừ phép, đổi ca) làm ở Application. Bảng: hr.hr_requests (+ hr_request_steps)
/// </summary>
public sealed class HrRequest : AggregateRoot<HrRequestId>, IBranchScoped, IAuditable
{
    /// <summary>DN-01.E8 — bổ sung đơn cho ngày đã qua tối đa N ngày</summary>
    public const int BackdateDays = 30;

    private readonly List<ApprovalStep> _steps = [];

    private HrRequest() { }

    public string Code { get; private set; } = null!;
    public HrRequestType Type { get; private set; }
    public EmployeeId EmployeeId { get; private set; }
    /// <summary>Chi nhánh chính của người gửi lúc gửi (phạm vi xem đơn)</summary>
    public BranchId BranchId { get; private set; }
    public DateOnly From { get; private set; }
    public DateOnly To { get; private set; }
    /// <summary>Số ngày nghỉ = ngày làm việc trong khoảng (nghỉ phép)</summary>
    public decimal? Days { get; private set; }
    public bool? PaidLeave { get; private set; }
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

    /// <summary>Bước đang chờ quyết định (null khi đơn đã xử lý)</summary>
    public ApprovalStep? CurrentStep => Status == HrRequestStatus.Pending ? _steps.OrderBy(s => s.Order).FirstOrDefault(s => s.Decision is null) : null;

    /// <summary>Có lương và đã duyệt → đã trừ phép</summary>
    public bool DeductsLeave => Type == HrRequestType.Leave && PaidLeave == true && Days > 0;

    /// <summary>Đơn chiếm ngày (nghỉ phép / công tác) — dùng kiểm trùng (DN-01.E4) và bảng công</summary>
    public bool BlocksDays => Type is HrRequestType.Leave or HrRequestType.BusinessTrip;

    public bool Covers(DateOnly date) => From <= date && date <= To;

    public static Result<HrRequest> Create(string code, HrRequestType type, EmployeeId employeeId, BranchId branchId, DateOnly from, DateOnly to,
        string reason, HrRequestDetails details, IReadOnlyList<PlannedStep> steps, DateTimeOffset at, DateOnly today)
    {
        if (to < from) return HrRequestErrors.InvalidDates;
        if (string.IsNullOrWhiteSpace(reason)) return HrRequestErrors.ReasonMissing;
        if (type is not (HrRequestType.Proposal or HrRequestType.Advance) && from < today.AddDays(-BackdateDays)) return HrRequestErrors.TooOld;

        var r = new HrRequest
        {
            Id = HrRequestId.New(), Code = code, Type = type, EmployeeId = employeeId, BranchId = branchId, From = from, To = to, Reason = reason.Trim(),
            RequestedAt = at, Status = HrRequestStatus.Pending,
        };
        switch (type)
        {
            case HrRequestType.Leave:
                var days = WorkDays(from, to);
                if (days == 0) return HrRequestErrors.NoWorkdays;
                (r.Days, r.PaidLeave) = (days, details.PaidLeave ?? true);
                break;
            case HrRequestType.LateEarly:
                if (from != to) return HrRequestErrors.InvalidDates;
                break;
            case HrRequestType.Advance:
                if (details.Amount is not { } adv || adv <= Money.Zero) return HrRequestErrors.AmountRequired;
                (r.Amount, r.Title) = (adv, Clean(details.Title));
                break;
            case HrRequestType.Proposal:
                if (string.IsNullOrWhiteSpace(details.Title)) return HrRequestErrors.TitleRequired;
                if (details.Amount is { } p && p < Money.Zero) return HrRequestErrors.AmountRequired;
                (r.Amount, r.Title) = (details.Amount is { } x && x > Money.Zero ? x : null, details.Title.Trim());
                break;
            case HrRequestType.ShiftSwap:
                if (details is not { SwapMyShiftId: { } mine, SwapWithEmployeeId: { } with, SwapTheirShiftId: { } theirs } || with == employeeId || mine == theirs)
                    return HrRequestErrors.InvalidSwap;
                (r.SwapMyShiftId, r.SwapWithEmployeeId, r.SwapTheirShiftId) = (mine, with, theirs);
                break;
        }

        var order = 1;
        foreach (var s in steps) r._steps.Add(new ApprovalStep(order++, s.Role, s.Approver?.Id, s.Approver?.Name));
        if (r._steps.Count == 0) r._steps.Add(new ApprovalStep(1, "Quản lý trực tiếp", null, null));
        return r;
    }

    /// <summary>Số ngày làm việc trong khoảng (bỏ Chủ nhật)</summary>
    public static int WorkDays(DateOnly from, DateOnly to)
    {
        var n = 0;
        for (var d = from; d <= to; d = d.AddDays(1)) if (d.DayOfWeek != DayOfWeek.Sunday) n++;
        return n;
    }

    /// <summary>Người được quyết định bước hiện tại: người duyệt dự kiến, hoặc người có `hr.requests.approve` (duyệt thay / bước mở)</summary>
    public bool CanDecide(Guid employeeId, bool canOverride) =>
        CurrentStep is { } step && employeeId != EmployeeId.Value && (step.ApproverId == employeeId || canOverride);

    /// <summary>
    /// DN-02 — quyết định bước hiện tại. Từ chối ở bất kỳ bước nào → đơn bị từ chối (bỏ các bước sau). Bước cuối duyệt → đơn đã duyệt
    /// (người gọi kiểm <see cref="Status"/> để áp dụng: trừ phép, đổi ca).
    /// </summary>
    public Result Decide(Guid byEmployeeId, string byName, bool canOverride, ApprovalDecision decision, string? comment, DateTimeOffset at)
    {
        if (Status != HrRequestStatus.Pending) return HrRequestErrors.NotPending;
        if (byEmployeeId == EmployeeId.Value) return HrRequestErrors.SelfApprove;
        var step = CurrentStep!;
        if (step.ApproverId != byEmployeeId && !canOverride) return HrRequestErrors.NotYourTurn;
        if (decision == ApprovalDecision.Rejected && string.IsNullOrWhiteSpace(comment)) return HrRequestErrors.ReasonRequired;

        step.Decide(decision, byEmployeeId, byName, onBehalf: step.ApproverId is { } planned && planned != byEmployeeId, Clean(comment), at);
        if (decision == ApprovalDecision.Rejected) Status = HrRequestStatus.Rejected;
        else if (_steps.All(s => s.Decision == ApprovalDecision.Approved)) Status = HrRequestStatus.Approved;
        return Result.Success();
    }

    /// <summary>DN-03 — người gửi hủy khi còn chờ duyệt</summary>
    public Result Cancel(EmployeeId by)
    {
        if (by != EmployeeId) return HrRequestErrors.OnlyOwner;
        if (Status != HrRequestStatus.Pending) return HrRequestErrors.NotPending;
        Status = HrRequestStatus.Cancelled;
        return Result.Success();
    }

    private static string? Clean(string? s) => string.IsNullOrWhiteSpace(s) ? null : s.Trim();
}
