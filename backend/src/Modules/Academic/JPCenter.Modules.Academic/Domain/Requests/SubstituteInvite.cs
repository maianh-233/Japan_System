using JPCenter.SharedKernel.Domain;
using JPCenter.SharedKernel.Primitives;
using JPCenter.Modules.Academic.Domain.Classes;
using JPCenter.Modules.Academic.Domain.ClassSessions;

namespace JPCenter.Modules.Academic.Domain.Requests;

/// <summary>
/// Lời mời GV khác dạy thay 1 buổi. Ai nhận TRƯỚC thì dạy — các lời mời còn lại của buổi tự hết hiệu lực (Application xử lý).
/// Bảng: academic.substitute_invites
/// </summary>
public sealed class SubstituteInvite : AggregateRoot<SubstituteInviteId>, IBranchScoped, IAuditable
{
    private SubstituteInvite() { }

    public ClassSessionId SessionId { get; private set; }
    public ClassId ClassId { get; private set; }
    public BranchId BranchId { get; private set; }
    /// <summary>Đơn hủy buổi dẫn tới lời mời</summary>
    public SessionCancelRequestId? CancelRequestId { get; private set; }
    /// <summary>GV vắng</summary>
    public Guid FromTeacherId { get; private set; }
    /// <summary>GV được mời</summary>
    public Guid ToTeacherId { get; private set; }
    public string Reason { get; private set; } = null!;
    /// <summary>Giáo vụ mời</summary>
    public Guid InvitedBy { get; private set; }
    public DateTimeOffset InvitedAt { get; private set; }
    /// <summary>Hạn trả lời</summary>
    public DateTimeOffset RespondBy { get; private set; }
    public InviteStatus Status { get; private set; }
    public DateTimeOffset? RespondedAt { get; private set; }
    public string? DeclineReason { get; private set; }

    public bool IsPending => Status == InviteStatus.Pending;

    public static Result<SubstituteInvite> Create(ClassSessionId sessionId, ClassId classId, BranchId branchId, SessionCancelRequestId? cancelRequestId,
        Guid fromTeacherId, Guid toTeacherId, string reason, Guid invitedBy, DateTimeOffset at, DateTimeOffset respondBy)
    {
        if (toTeacherId == fromTeacherId || respondBy <= at) return RequestErrors.InviteInvalid;
        return new SubstituteInvite
        {
            Id = SubstituteInviteId.New(), SessionId = sessionId, ClassId = classId, BranchId = branchId, CancelRequestId = cancelRequestId,
            FromTeacherId = fromTeacherId, ToTeacherId = toTeacherId, Reason = reason, InvitedBy = invitedBy, InvitedAt = at, RespondBy = respondBy,
            Status = InviteStatus.Pending,
        };
    }

    public Result Accept(DateTimeOffset at)
    {
        if (Status != InviteStatus.Pending) return RequestErrors.InviteClosed;
        if (at > RespondBy) { Status = InviteStatus.Expired; return RequestErrors.InviteClosed; }
        (Status, RespondedAt) = (InviteStatus.Accepted, at);
        return Result.Success();
    }

    public Result Decline(string? reason, DateTimeOffset at)
    {
        if (Status != InviteStatus.Pending) return RequestErrors.InviteClosed;
        (Status, RespondedAt, DeclineReason) = (InviteStatus.Declined, at, string.IsNullOrWhiteSpace(reason) ? null : reason.Trim());
        return Result.Success();
    }

    /// <summary>Hết hiệu lực: GV khác đã nhận / giáo vụ thu hồi / quá hạn — lý do ghi vào <see cref="DeclineReason"/></summary>
    public void Expire(string reason, DateTimeOffset at)
    {
        if (Status == InviteStatus.Pending) (Status, RespondedAt, DeclineReason) = (InviteStatus.Expired, at, reason);
    }

    /// <summary>Job: quá hạn trả lời mà chưa ai nhận (DT-16 bước 3)</summary>
    public bool ExpireIfOverdue(DateTimeOffset now)
    {
        if (Status != InviteStatus.Pending || now <= RespondBy) return false;
        Expire("Quá hạn trả lời", now);
        return true;
    }
}
