using JPCenter.SharedKernel.Domain;
using JPCenter.SharedKernel.Primitives;

namespace JPCenter.Modules.System.Domain.Approvals;

/// <summary>
/// Duyệt 2 người (maker–checker) cho hoàn tiền, hủy hóa đơn, cấp quyền tài chính. Bảng: system.approval_requests.
/// Người tạo không tự duyệt. Duyệt xong → phát event để module sở hữu thực hiện thao tác.
/// </summary>
public sealed class ApprovalRequest : AggregateRoot<ApprovalRequestId>, IAuditable
{
    private ApprovalRequest() { }

    /// <summary>Mã hiển thị, vd DY-0012</summary>
    public string Code { get; private set; } = null!;
    public ApprovalKind Kind { get; private set; }
    public string Title { get; private set; } = null!;
    public string? Detail { get; private set; }
    public BranchId? BranchId { get; private set; }
    /// <summary>Dữ liệu thao tác cần thực hiện khi duyệt (jsonb, cấu trúc theo Kind)</summary>
    public string Payload { get; private set; } = null!;
    public Guid RequestedById { get; private set; }
    public string RequestedByName { get; private set; } = null!;
    public DateTimeOffset RequestedAt { get; private set; }
    public ApprovalStatus Status { get; private set; }
    public Guid? DecidedById { get; private set; }
    public string? DecidedByName { get; private set; }
    public DateTimeOffset? DecidedAt { get; private set; }
    /// <summary>Lý do từ chối / ghi chú khi duyệt</summary>
    public string? Note { get; private set; }

    public static ApprovalRequest Create(string code, ApprovalKind kind, string title, string? detail, BranchId? branchId,
        string payload, Guid requestedById, string requestedByName, DateTimeOffset at) =>
        new()
        {
            Id = ApprovalRequestId.New(), Code = code, Kind = kind, Title = title, Detail = detail, BranchId = branchId, Payload = payload,
            RequestedById = requestedById, RequestedByName = requestedByName, RequestedAt = at, Status = ApprovalStatus.Pending,
        };

    public Result Approve(Guid byId, string byName, string? note, DateTimeOffset at) => Decide(ApprovalStatus.Approved, byId, byName, note, at);

    public Result Reject(Guid byId, string byName, string note, DateTimeOffset at) => Decide(ApprovalStatus.Rejected, byId, byName, note, at);

    public Result Withdraw(Guid byId, DateTimeOffset at)
    {
        if (Status != ApprovalStatus.Pending) return ApprovalErrors.NotPending;
        if (byId != RequestedById) return ApprovalErrors.OnlyRequester;
        (Status, DecidedAt) = (ApprovalStatus.Withdrawn, at);
        return Result.Success();
    }

    private Result Decide(ApprovalStatus status, Guid byId, string byName, string? note, DateTimeOffset at)
    {
        if (Status != ApprovalStatus.Pending) return ApprovalErrors.NotPending;
        if (byId == RequestedById) return ApprovalErrors.SelfApproval;
        (Status, DecidedById, DecidedByName, DecidedAt, Note) = (status, byId, byName, at, note);
        return Result.Success();
    }
}
