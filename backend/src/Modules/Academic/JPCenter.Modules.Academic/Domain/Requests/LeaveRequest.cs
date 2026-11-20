using JPCenter.SharedKernel.Domain;
using JPCenter.SharedKernel.Primitives;
using JPCenter.Modules.Academic.Domain.Classes;
using JPCenter.Modules.Academic.Domain.ClassSessions;

namespace JPCenter.Modules.Academic.Domain.Requests;

/// <summary>
/// Đơn xin nghỉ học của HV / PH cho 1 buổi; có thể kèm đăng ký học bù ở lớp khác cùng trình độ.
/// Bảng: academic.leave_requests
/// </summary>
public sealed class LeaveRequest : AggregateRoot<LeaveRequestId>, IBranchScoped, IAuditable
{
    private LeaveRequest() { }

    /// <summary>Students.Student</summary>
    public Guid StudentId { get; private set; }
    public ClassId ClassId { get; private set; }
    public ClassSessionId SessionId { get; private set; }
    public BranchId BranchId { get; private set; }
    public string ReasonType { get; private set; } = null!;
    public string Reason { get; private set; } = null!;
    public RequestedBy By { get; private set; }
    /// <summary>Tài khoản cổng gửi đơn (System.PortalAccount)</summary>
    public Guid ByAccountId { get; private set; }
    public string ByName { get; private set; } = null!;
    public RequestStatus Status { get; private set; }
    public DateTimeOffset RequestedAt { get; private set; }
    public Guid? DecidedBy { get; private set; }
    public DateTimeOffset? DecidedAt { get; private set; }
    /// <summary>Buổi học bù đã đăng ký (lớp khác)</summary>
    public ClassSessionId? MakeupSessionId { get; private set; }
    /// <summary>Kết quả điểm danh ở buổi học bù</summary>
    public AttendanceStatus? MakeupResult { get; private set; }
    public string? Note { get; private set; }

    /// <summary>Chi tiết lý do tối thiểu (DT-18)</summary>
    public const int MinReasonLength = 5;

    public bool IsPending => Status == RequestStatus.Pending;

    /// <summary>
    /// HV / PH gửi đơn (DT-18): buổi chưa bắt đầu, lý do ≥ 5 ký tự. Buổi học bù (nếu có) đã kiểm bằng <see cref="MakeupRules"/>;
    /// trùng đơn (E2), PH không liên kết (E5) — handler kiểm.
    /// </summary>
    public static Result<LeaveRequest> Create(Guid studentId, ClassId classId, ClassSessionId sessionId, BranchId branchId, string reasonType,
        string reason, RequestedBy by, Guid byAccountId, string byName, ClassSessionId? makeupSessionId, DateTime sessionStartsAt, DateTime now,
        DateTimeOffset at)
    {
        if (now >= sessionStartsAt) return RequestErrors.LeaveTooLate;
        if (string.IsNullOrWhiteSpace(reasonType) || (reason?.Trim().Length ?? 0) < MinReasonLength) return RequestErrors.LeaveReasonRequired;
        return new LeaveRequest
        {
            Id = LeaveRequestId.New(), StudentId = studentId, ClassId = classId, SessionId = sessionId, BranchId = branchId, ReasonType = reasonType.Trim(),
            Reason = reason!.Trim(), By = by, ByAccountId = byAccountId, ByName = byName, MakeupSessionId = makeupSessionId, RequestedAt = at,
            Status = RequestStatus.Pending,
        };
    }

    /// <summary>Người gửi thu hồi khi còn chờ duyệt</summary>
    public Result Withdraw(DateTimeOffset at)
    {
        if (!IsPending) return RequestErrors.NotPending;
        (Status, DecidedAt) = (RequestStatus.Withdrawn, at);
        return Result.Success();
    }

    /// <summary>HV / PH đổi hoặc bỏ buổi học bù khi đơn còn chờ duyệt (buổi bù đã kiểm bằng <see cref="MakeupRules"/>)</summary>
    public Result ChangeMakeup(ClassSessionId? makeupSessionId)
    {
        if (!IsPending) return RequestErrors.NotPending;
        (MakeupSessionId, MakeupResult) = (makeupSessionId, null);
        return Result.Success();
    }

    /// <summary>Buổi bù đầy khi duyệt (DT-18.E3): vẫn duyệt nghỉ, bỏ đăng ký bù</summary>
    public void DropMakeup() => (MakeupSessionId, MakeupResult) = (null, null);

    public Result Approve(Guid by, DateTimeOffset at, string? note) => Decide(RequestStatus.Approved, by, at, note);
    public Result Reject(Guid by, DateTimeOffset at, string note) =>
        string.IsNullOrWhiteSpace(note) ? RequestErrors.RejectReasonRequired : Decide(RequestStatus.Rejected, by, at, note.Trim());
    public void RecordMakeupResult(AttendanceStatus result)
    {
        if (Status == RequestStatus.Approved && MakeupSessionId is not null) MakeupResult = result;
    }

    private Result Decide(RequestStatus status, Guid by, DateTimeOffset at, string? note)
    {
        if (Status != RequestStatus.Pending) return RequestErrors.NotPending;
        (Status, DecidedBy, DecidedAt, Note) = (status, by, at, note);
        return Result.Success();
    }
}
