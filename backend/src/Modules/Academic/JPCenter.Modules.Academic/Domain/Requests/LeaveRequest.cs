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

    public static LeaveRequest Create(Guid studentId, ClassId classId, ClassSessionId sessionId, BranchId branchId, string reasonType,
        string reason, RequestedBy by, Guid byAccountId, string byName, ClassSessionId? makeupSessionId, DateTimeOffset at) =>
        new()
        {
            Id = LeaveRequestId.New(), StudentId = studentId, ClassId = classId, SessionId = sessionId, BranchId = branchId, ReasonType = reasonType,
            Reason = reason, By = by, ByAccountId = byAccountId, ByName = byName, MakeupSessionId = makeupSessionId, RequestedAt = at,
            Status = RequestStatus.Pending,
        };

    public Result Approve(Guid by, DateTimeOffset at, string? note) => Decide(RequestStatus.Approved, by, at, note);
    public Result Reject(Guid by, DateTimeOffset at, string note) => Decide(RequestStatus.Rejected, by, at, note);
    public void RecordMakeupResult(AttendanceStatus result) => MakeupResult = result;

    private Result Decide(RequestStatus status, Guid by, DateTimeOffset at, string? note)
    {
        if (Status != RequestStatus.Pending) return RequestErrors.NotPending;
        (Status, DecidedBy, DecidedAt, Note) = (status, by, at, note);
        return Result.Success();
    }
}
