using JPCenter.SharedKernel.Domain;
using JPCenter.SharedKernel.Primitives;

namespace JPCenter.Modules.Academic.Domain.Rooms;

/// <summary>Báo hư hỏng phòng / thiết bị: open → in_progress → resolved. Bảng: academic.room_issues</summary>
public sealed class RoomIssue : AggregateRoot<RoomIssueId>, IAuditable
{
    private RoomIssue() { }

    public RoomId RoomId { get; private set; }
    /// <summary>Thiết bị hỏng — null = cơ sở vật chất khác</summary>
    public Equipment? Equipment { get; private set; }
    public string Title { get; private set; } = null!;
    public string? Description { get; private set; }
    public IssueSeverity Severity { get; private set; }
    public IssueStatus Status { get; private set; }
    public Guid ReportedBy { get; private set; }
    public DateTimeOffset ReportedAt { get; private set; }
    /// <summary>Người / đơn vị nhận sửa</summary>
    public string? Assignee { get; private set; }
    public DateOnly? DueDate { get; private set; }
    public DateTimeOffset? StartedAt { get; private set; }
    public DateTimeOffset? ResolvedAt { get; private set; }
    public string? Resolution { get; private set; }
    public Money? Cost { get; private set; }

    public static RoomIssue Report(RoomId roomId, Equipment? equipment, string title, string? description, IssueSeverity severity,
        Guid reportedBy, DateTimeOffset at) =>
        new()
        {
            Id = RoomIssueId.New(), RoomId = roomId, Equipment = equipment, Title = title, Description = description, Severity = severity,
            ReportedBy = reportedBy, ReportedAt = at, Status = IssueStatus.Open,
        };

    public Result Start(string assignee, DateOnly? dueDate, DateTimeOffset at)
    {
        if (Status != IssueStatus.Open) return Error.Conflict("academic.room_issue.not_open", "Báo hỏng đã được nhận xử lý");
        (Status, Assignee, DueDate, StartedAt) = (IssueStatus.InProgress, assignee, dueDate, at);
        return Result.Success();
    }

    public Result Resolve(string resolution, Money? cost, DateTimeOffset at)
    {
        if (Status == IssueStatus.Resolved) return Error.Conflict("academic.room_issue.resolved", "Báo hỏng đã xử lý xong");
        (Status, Resolution, Cost, ResolvedAt) = (IssueStatus.Resolved, resolution, cost, at);
        return Result.Success();
    }

    /// <summary>Sửa chưa xong / hỏng lại → mở lại (giữ người nhận sửa, xóa kết quả)</summary>
    public Result Reopen()
    {
        if (Status != IssueStatus.Resolved) return Error.Conflict("academic.room_issue.not_resolved", "Báo hỏng chưa xử lý xong");
        Status = Assignee is null ? IssueStatus.Open : IssueStatus.InProgress;
        (Resolution, Cost, ResolvedAt) = (null, null, null);
        return Result.Success();
    }
}
