using JPCenter.SharedKernel.Domain;
using JPCenter.SharedKernel.Primitives;
using JPCenter.Modules.Academic.Domain.Classes;
using JPCenter.Modules.Academic.Domain.ClassSessions;

namespace JPCenter.Modules.Academic.Domain.Requests;

/// <summary>
/// GV xin hủy 1 buổi (chỉ trung tâm hủy được cả lớp). Giáo vụ duyệt → mời GV dạy thay HOẶC hủy buổi & xếp học bù.
/// Bảng: academic.session_cancel_requests
/// </summary>
public sealed class SessionCancelRequest : AggregateRoot<SessionCancelRequestId>, IBranchScoped, IAuditable
{
    private SessionCancelRequest() { }

    public ClassSessionId SessionId { get; private set; }
    public ClassId ClassId { get; private set; }
    public BranchId BranchId { get; private set; }
    /// <summary>GV gửi đơn (Hr.Employee)</summary>
    public Guid TeacherId { get; private set; }
    /// <summary>Loại lý do (ốm, việc gia đình...)</summary>
    public string ReasonType { get; private set; } = null!;
    public string Reason { get; private set; } = null!;
    /// <summary>GV đề xuất ngày dạy bù</summary>
    public DateTimeOffset? ProposedMakeupAt { get; private set; }
    public RequestStatus Status { get; private set; }
    public DateTimeOffset RequestedAt { get; private set; }
    public Guid? DecidedBy { get; private set; }
    public DateTimeOffset? DecidedAt { get; private set; }
    /// <summary>Kết quả xử lý: "Đã mời GV ... dạy thay"</summary>
    public string? Note { get; private set; }

    /// <summary>Chi tiết lý do tối thiểu (DT-16)</summary>
    public const int MinReasonLength = 10;

    public bool IsPending => Status == RequestStatus.Pending;

    /// <summary>
    /// GV gửi đơn (DT-16): buổi chưa bắt đầu (<paramref name="sessionStartsAt"/>, <paramref name="now"/> theo giờ VN), lý do ≥ 10 ký tự.
    /// Trùng đơn chờ duyệt (E7) — handler kiểm.
    /// </summary>
    public static Result<SessionCancelRequest> Create(ClassSessionId sessionId, ClassId classId, BranchId branchId, Guid teacherId,
        string reasonType, string reason, DateTimeOffset? proposedMakeupAt, DateTime sessionStartsAt, DateTime now, DateTimeOffset at)
    {
        if (now >= sessionStartsAt) return RequestErrors.CancelSessionStarted;
        if (string.IsNullOrWhiteSpace(reasonType) || (reason?.Trim().Length ?? 0) < MinReasonLength) return RequestErrors.CancelReasonRequired;
        return new SessionCancelRequest
        {
            Id = SessionCancelRequestId.New(), SessionId = sessionId, ClassId = classId, BranchId = branchId, TeacherId = teacherId,
            ReasonType = reasonType.Trim(), Reason = reason!.Trim(), ProposedMakeupAt = proposedMakeupAt, RequestedAt = at, Status = RequestStatus.Pending,
        };
    }

    /// <summary>GV thu hồi khi còn chờ duyệt</summary>
    public Result Withdraw(DateTimeOffset at)
    {
        if (!IsPending) return RequestErrors.NotPending;
        (Status, DecidedAt) = (RequestStatus.Withdrawn, at);
        return Result.Success();
    }

    /// <summary>Buổi / lớp liên kết không còn (DT-16.E6) → đóng đơn</summary>
    public void CloseOrphan(DateTimeOffset at)
    {
        if (IsPending) (Status, DecidedAt, Note) = (RequestStatus.Rejected, at, RequestErrors.SessionGone.Message);
    }

    public Result Approve(Guid by, DateTimeOffset at, string? note) => Decide(RequestStatus.Approved, by, at, note);
    public Result Reject(Guid by, DateTimeOffset at, string note) =>
        string.IsNullOrWhiteSpace(note) ? RequestErrors.RejectReasonRequired : Decide(RequestStatus.Rejected, by, at, note.Trim());
    public void AddResultNote(string note) => Note = note;

    private Result Decide(RequestStatus status, Guid by, DateTimeOffset at, string? note)
    {
        if (Status != RequestStatus.Pending) return RequestErrors.NotPending;
        (Status, DecidedBy, DecidedAt, Note) = (status, by, at, note);
        return Result.Success();
    }
}
