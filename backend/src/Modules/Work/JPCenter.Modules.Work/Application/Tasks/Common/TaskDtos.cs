using FluentValidation;
using JPCenter.Modules.Work.Domain.Tasks;

namespace JPCenter.Modules.Work.Application.Tasks.Common;

public sealed record TaskLinkDto(TaskLinkType Type, Guid Id, string Label);

public sealed record ChecklistItemDto(Guid Id, string Text, bool Done);

/// <summary><c>by</c> = tên người viết lúc viết (như frontend), thêm <c>byId</c></summary>
public sealed record TaskCommentDto(DateTimeOffset At, string By, Guid ById, string Text);

/// <summary>Task — khớp <c>Task</c> frontend; thêm chi nhánh và quyền của người đang xem (sửa nội dung / xóa / đổi trạng thái).</summary>
public sealed record TaskDto(Guid Id, string Code, string Title, string? Description, Guid AssigneeId, Guid CreatorId, IReadOnlyList<Guid> WatcherIds,
    TaskPriority Priority, WorkTaskStatus Status, DateOnly? Due, TaskRepeat Repeat, TaskLinkDto? Link, IReadOnlyList<ChecklistItemDto> Checklist,
    IReadOnlyList<TaskCommentDto> Comments, DateTimeOffset CreatedAt, DateTimeOffset? CompletedAt, Guid BranchId, bool CanEdit, bool CanDelete,
    bool CanMove);

/// <summary>CV-02 — quá hạn, hạn hôm nay, chờ duyệt, xong trong 7 ngày (trên tập đang xem)</summary>
public sealed record TaskStatsDto(int Overdue, int DueToday, int Review, int DoneWeek);

public sealed record TaskListDto(IReadOnlyList<TaskDto> Items, TaskStatsDto Stats);

/// <summary>Kết quả đổi trạng thái: task + lần lặp kế tiếp vừa sinh (CV-02, task lặp)</summary>
public sealed record TaskMoveResult(TaskDto Task, TaskDto? Next);

/// <summary>Gắn task với lead / lớp / HV — server tự đặt nhãn</summary>
public sealed record TaskLinkInput(TaskLinkType Type, Guid Id);

/// <summary>Form task (CV-01). <c>checklist</c> chỉ dùng khi tạo.</summary>
public sealed record TaskInput(string Title, string? Description, Guid AssigneeId, List<Guid>? WatcherIds, TaskPriority Priority, DateOnly? Due,
    TaskRepeat Repeat, TaskLinkInput? Link, List<string>? Checklist);

public sealed class TaskInputValidator : AbstractValidator<TaskInput>
{
    public TaskInputValidator()
    {
        RuleFor(x => x.Title).NotEmpty().WithErrorCode("work.task.title_required").WithMessage("Chưa nhập tiêu đề công việc")
            .MaximumLength(WorkTask.TitleMax);
        RuleFor(x => x.Description).MaximumLength(4000);
        RuleFor(x => x.Due).NotNull().When(x => x.Repeat != TaskRepeat.None).WithErrorCode("work.task.repeat_needs_due")
            .WithMessage("Task lặp lại cần có hạn");
        RuleForEach(x => x.Checklist).MaximumLength(300);
    }
}
