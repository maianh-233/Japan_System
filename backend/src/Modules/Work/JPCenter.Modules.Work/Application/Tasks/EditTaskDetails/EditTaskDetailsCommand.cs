using FluentValidation;
using JPCenter.Application.Common.Messaging;
using JPCenter.Modules.Work.Application.Abstractions;
using JPCenter.Modules.Work.Application.Common;
using JPCenter.Modules.Work.Application.Tasks.Common;
using JPCenter.Modules.Work.Domain.Tasks;
using JPCenter.SharedKernel.Domain;
using JPCenter.SharedKernel.Primitives;

namespace JPCenter.Modules.Work.Application.Tasks.EditTaskDetails;

/// <summary>Thao tác chi tiết task của người dính tới task (CV-05)</summary>
public enum TaskDetailAction
{
    /// <summary>Thêm mục checklist (<c>Text</c>)</summary>
    AddItem,
    /// <summary>Tick / bỏ tick mục (<c>ItemId</c>, <c>Done</c>)</summary>
    TickItem,
    /// <summary>Xóa mục (<c>ItemId</c>)</summary>
    RemoveItem,
    /// <summary>Bình luận (<c>Text</c> ≤ 2000) — thông báo những người còn lại</summary>
    Comment,
}

/// <summary>CV-05 — checklist + bình luận (người làm / giao / theo dõi, quản lý người làm, <c>work.tasks.update</c>).</summary>
public sealed record EditTaskDetailsCommand(Guid Id, TaskDetailAction Action, string? Text, Guid? ItemId, bool Done) : ICommand<TaskDto>;

public sealed class EditTaskDetailsValidator : AbstractValidator<EditTaskDetailsCommand>
{
    public EditTaskDetailsValidator() => RuleFor(x => x.Text).MaximumLength(WorkTask.CommentMax);
}

public sealed class EditTaskDetailsHandler(IWorkDbContext db, TaskDesk desk, WorkPeople people, WorkNotices notices, IClock clock)
    : ICommandHandler<EditTaskDetailsCommand, TaskDto>
{
    public async Task<Result<TaskDto>> Handle(EditTaskDetailsCommand cmd, CancellationToken ct)
    {
        var t = await desk.FindAsync(cmd.Id, ct);
        if (t is null) return WorkTaskErrors.NotFound;
        if (!await desk.CanMoveAsync(t, ct)) return WorkTaskErrors.Forbidden;

        var done = cmd.Action switch
        {
            TaskDetailAction.AddItem => t.AddChecklistItem(cmd.Text ?? ""),
            TaskDetailAction.TickItem => cmd.ItemId is { } tick ? t.TickChecklist(tick, cmd.Done) : WorkTaskErrors.ItemNotFound,
            TaskDetailAction.RemoveItem => cmd.ItemId is { } remove ? t.RemoveChecklistItem(remove) : WorkTaskErrors.ItemNotFound,
            _ => t.AddComment(cmd.Text ?? "", people.MeId ?? Guid.Empty, people.MeName, clock.UtcNow),
        };
        if (done.IsFailure) return done.Error;

        if (cmd.Action == TaskDetailAction.Comment)
        {
            var others = new[] { t.AssigneeId, t.CreatorId }.Concat(t.WatcherIds).Where(x => x != people.MeId);
            notices.ToEmployees(others, $"Bình luận mới · {t.Code}", $"{people.MeName}: {Short(cmd.Text!.Trim())}", "info", WorkNotices.TasksPage,
                "work.task.comment");
        }
        await db.SaveChangesAsync(ct);
        return await desk.ToDtoAsync(t, ct);
    }

    private static string Short(string s) => s.Length <= 120 ? s : s[..117] + "...";
}
