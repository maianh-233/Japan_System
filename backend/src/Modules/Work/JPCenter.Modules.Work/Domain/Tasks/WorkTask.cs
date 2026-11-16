using JPCenter.SharedKernel.Domain;
using JPCenter.SharedKernel.Primitives;

namespace JPCenter.Modules.Work.Domain.Tasks;

/// <summary>
/// Công việc giao cho nhân viên (Kanban): hạn, ưu tiên, người theo dõi, lặp lại, checklist, bình luận, gắn lead / lớp / HV.
/// Bảng: work.tasks (+ task_checklist_items, task_comments)
/// </summary>
public sealed class WorkTask : AggregateRoot<WorkTaskId>, IBranchScoped, IAuditable, ISoftDeletable
{
    private readonly List<TaskChecklistItem> _checklist = [];
    private readonly List<TaskComment> _comments = [];

    private WorkTask() { }

    public string Code { get; private set; } = null!;
    public string Title { get; private set; } = null!;
    public string? Description { get; private set; }
    public BranchId BranchId { get; private set; }
    /// <summary>Người làm (Hr.Employee)</summary>
    public Guid AssigneeId { get; private set; }
    /// <summary>Người giao (Hr.Employee)</summary>
    public Guid CreatorId { get; private set; }
    public List<Guid> WatcherIds { get; private set; } = [];
    public TaskPriority Priority { get; private set; }
    public WorkTaskStatus Status { get; private set; }
    public DateTimeOffset? Due { get; private set; }
    public TaskRepeat Repeat { get; private set; }
    /// <summary>Gắn đối tượng nghiệp vụ</summary>
    public TaskLinkType? LinkType { get; private set; }
    public Guid? LinkId { get; private set; }
    public string? LinkLabel { get; private set; }
    public DateTimeOffset? CompletedAt { get; private set; }
    public IReadOnlyList<TaskChecklistItem> Checklist => _checklist;
    public IReadOnlyList<TaskComment> Comments => _comments;

    public static Result<WorkTask> Create(string code, string title, string? description, BranchId branchId, Guid assigneeId, Guid creatorId,
        List<Guid> watcherIds, TaskPriority priority, DateTimeOffset? due, TaskRepeat repeat)
    {
        if (string.IsNullOrWhiteSpace(title)) return Error.Validation("work.task.title_required", "Chưa nhập tiêu đề công việc");
        return new WorkTask
        {
            Id = WorkTaskId.New(), Code = code, Title = title, Description = description, BranchId = branchId, AssigneeId = assigneeId,
            CreatorId = creatorId, WatcherIds = watcherIds, Priority = priority, Due = due, Repeat = repeat, Status = WorkTaskStatus.Todo,
        };
    }

    public void LinkTo(TaskLinkType type, Guid id, string label) => (LinkType, LinkId, LinkLabel) = (type, id, label);
    public void Reassign(Guid assigneeId) => AssigneeId = assigneeId;

    public void Move(WorkTaskStatus status, DateTimeOffset at)
    {
        Status = status;
        CompletedAt = status == WorkTaskStatus.Done ? at : null;
    }

    public void AddChecklistItem(string text) => _checklist.Add(new TaskChecklistItem(text, _checklist.Count + 1));
    public void TickChecklist(Guid itemId, bool done) => _checklist.FirstOrDefault(i => i.Id == itemId)?.Set(done);
    public void AddComment(string text, Guid by, DateTimeOffset at) => _comments.Add(new TaskComment(text, by, at));

    /// <summary>Task lặp: tạo bản kế tiếp khi hoàn thành</summary>
    public WorkTask? NextOccurrence(string code)
    {
        if (Repeat == TaskRepeat.None || Due is not { } due) return null;
        var next = Repeat switch { TaskRepeat.Daily => due.AddDays(1), TaskRepeat.Weekly => due.AddDays(7), _ => due.AddMonths(1) };
        var t = Create(code, Title, Description, BranchId, AssigneeId, CreatorId, [.. WatcherIds], Priority, next, Repeat).Value;
        foreach (var i in _checklist) t.AddChecklistItem(i.Text);
        if (LinkType is { } lt) t.LinkTo(lt, LinkId!.Value, LinkLabel!);
        return t;
    }
}
