using JPCenter.SharedKernel.Domain;
using JPCenter.SharedKernel.Primitives;

namespace JPCenter.Modules.Work.Domain.Tasks;

/// <summary>
/// Công việc giao cho nhân viên (Kanban, CV-01 → CV-05): hạn, ưu tiên, người theo dõi, lặp lại, checklist, bình luận, gắn lead / lớp / HV.
/// Bảng: work.tasks (+ task_checklist_items, task_comments)
/// </summary>
public sealed class WorkTask : AggregateRoot<WorkTaskId>, IBranchScoped, IAuditable, ISoftDeletable
{
    public const int TitleMax = 200;
    public const int CommentMax = 2000;

    private readonly List<TaskChecklistItem> _checklist = [];
    private readonly List<TaskComment> _comments = [];

    private WorkTask() { }

    public string Code { get; private set; } = null!;
    public string Title { get; private set; } = null!;
    public string? Description { get; private set; }
    /// <summary>Chi nhánh của người làm (phạm vi xem)</summary>
    public BranchId BranchId { get; private set; }
    /// <summary>Người làm (Hr.Employee)</summary>
    public Guid AssigneeId { get; private set; }
    /// <summary>Người giao (Hr.Employee)</summary>
    public Guid CreatorId { get; private set; }
    public List<Guid> WatcherIds { get; private set; } = [];
    public TaskPriority Priority { get; private set; }
    public WorkTaskStatus Status { get; private set; }
    public DateOnly? Due { get; private set; }
    public TaskRepeat Repeat { get; private set; }
    /// <summary>Gắn đối tượng nghiệp vụ (Crm.Lead / Academic.Class / Students.Student)</summary>
    public TaskLinkType? LinkType { get; private set; }
    public Guid? LinkId { get; private set; }
    public string? LinkLabel { get; private set; }
    public DateTimeOffset? CompletedAt { get; private set; }
    /// <summary>CV-02.E2 — task lặp đã sinh lần kế tiếp (chỉ sinh 1 lần)</summary>
    public bool NextSpawned { get; private set; }
    public IReadOnlyList<TaskChecklistItem> Checklist => _checklist;
    public IReadOnlyList<TaskComment> Comments => _comments;

    public bool IsDone => Status == WorkTaskStatus.Done;

    /// <summary>Người làm / người giao / người theo dõi</summary>
    public bool Involves(Guid employeeId) => AssigneeId == employeeId || CreatorId == employeeId || WatcherIds.Contains(employeeId);

    public static Result<WorkTask> Create(string code, string title, string? description, BranchId branchId, Guid assigneeId, Guid creatorId,
        IEnumerable<Guid> watcherIds, TaskPriority priority, DateOnly? due, TaskRepeat repeat, IEnumerable<string>? checklist = null)
    {
        var t = new WorkTask { Id = WorkTaskId.New(), Code = code, CreatorId = creatorId, Status = WorkTaskStatus.Todo };
        var set = t.Update(title, description, priority, due, repeat, watcherIds);
        if (set.IsFailure) return set.Error;
        t.Reassign(assigneeId, branchId);
        foreach (var item in checklist ?? []) if (!string.IsNullOrWhiteSpace(item)) t._checklist.Add(new TaskChecklistItem(item.Trim(), t._checklist.Count + 1));
        return t;
    }

    /// <summary>CV-01 — sửa nội dung</summary>
    public Result Update(string title, string? description, TaskPriority priority, DateOnly? due, TaskRepeat repeat, IEnumerable<Guid> watcherIds)
    {
        if (string.IsNullOrWhiteSpace(title) || title.Trim().Length > TitleMax) return WorkTaskErrors.TitleRequired;
        if (repeat != TaskRepeat.None && due is null) return WorkTaskErrors.RepeatNeedsDue;
        (Title, Description, Priority, Due, Repeat) = (title.Trim(), string.IsNullOrWhiteSpace(description) ? null : description.Trim(), priority, due, repeat);
        WatcherIds = [.. watcherIds.Distinct().Where(w => w != AssigneeId)];
        return Result.Success();
    }

    /// <summary>CV-01.E5 — đổi người làm (chi nhánh theo người làm mới)</summary>
    public void Reassign(Guid assigneeId, BranchId branchId)
    {
        (AssigneeId, BranchId) = (assigneeId, branchId);
        WatcherIds.Remove(assigneeId);
    }

    public void LinkTo(TaskLinkType type, Guid id, string label) => (LinkType, LinkId, LinkLabel) = (type, id, label);
    public void Unlink() => (LinkType, LinkId, LinkLabel) = (null, null, null);

    /// <summary>
    /// CV-02 — đổi cột. Hoàn thành → ghi giờ xong; mở lại → bỏ giờ xong. Task lặp hoàn thành lần đầu → trả lần kế tiếp
    /// (người gọi đặt mã + lưu); đã sinh rồi → null.
    /// </summary>
    public WorkTask? Move(WorkTaskStatus status, DateTimeOffset at, DateOnly today, Func<string> nextCode)
    {
        if (Status == status) return null;
        Status = status;
        CompletedAt = status == WorkTaskStatus.Done ? at : null;
        if (status != WorkTaskStatus.Done || Repeat == TaskRepeat.None || NextSpawned) return null;
        NextSpawned = true;
        return NextOccurrence(nextCode(), today);
    }

    private WorkTask NextOccurrence(string code, DateOnly today)
    {
        var baseDue = Due ?? today;
        var next = Repeat switch { TaskRepeat.Daily => baseDue.AddDays(1), TaskRepeat.Weekly => baseDue.AddDays(7), _ => baseDue.AddMonths(1) };
        var t = Create(code, Title, Description, BranchId, AssigneeId, CreatorId, [.. WatcherIds], Priority, next, Repeat, _checklist.OrderBy(i => i.Order).Select(i => i.Text)).Value;
        if (LinkType is { } lt) t.LinkTo(lt, LinkId!.Value, LinkLabel!);
        return t;
    }

    public Result AddChecklistItem(string text)
    {
        if (string.IsNullOrWhiteSpace(text)) return WorkTaskErrors.ItemRequired;
        _checklist.Add(new TaskChecklistItem(text.Trim(), _checklist.Count == 0 ? 1 : _checklist.Max(i => i.Order) + 1));
        return Result.Success();
    }

    public Result TickChecklist(Guid itemId, bool done)
    {
        if (_checklist.FirstOrDefault(i => i.Id == itemId) is not { } item) return WorkTaskErrors.ItemNotFound;
        item.Set(done);
        return Result.Success();
    }

    public Result RemoveChecklistItem(Guid itemId) => _checklist.RemoveAll(i => i.Id == itemId) > 0 ? Result.Success() : WorkTaskErrors.ItemNotFound;

    /// <summary>CV-05</summary>
    public Result AddComment(string text, Guid by, string byName, DateTimeOffset at)
    {
        if (string.IsNullOrWhiteSpace(text) || text.Trim().Length > CommentMax) return WorkTaskErrors.CommentInvalid;
        _comments.Add(new TaskComment(text.Trim(), by, byName, at));
        return Result.Success();
    }
}
