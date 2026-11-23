using FluentValidation;
using JPCenter.Application.Common.Messaging;
using JPCenter.Modules.Work.Application.Abstractions;
using JPCenter.Modules.Work.Application.Common;
using JPCenter.Modules.Work.Application.Tasks.Common;
using JPCenter.Modules.Work.Domain.Tasks;
using JPCenter.SharedKernel.Domain;
using JPCenter.SharedKernel.Primitives;

namespace JPCenter.Modules.Work.Application.Tasks.ChangeTaskStatus;

/// <summary>
/// CV-02 / CV-04 — kéo thẻ / đổi trạng thái (kèm bình luận tùy chọn, vd lý do "Yêu cầu làm lại"). Hoàn thành task lặp lần đầu → sinh lần kế tiếp (E2).
/// </summary>
public sealed record ChangeTaskStatusCommand(Guid Id, WorkTaskStatus Status, string? Comment) : ICommand<TaskMoveResult>;

public sealed class ChangeTaskStatusValidator : AbstractValidator<ChangeTaskStatusCommand>
{
    public ChangeTaskStatusValidator() => RuleFor(x => x.Comment).MaximumLength(WorkTask.CommentMax);
}

/// <summary>
/// Người làm chuyển Chờ duyệt → người giao nhận thông báo; người khác chuyển Hoàn thành → người làm nhận thông báo;
/// mở lại task đã xong (Yêu cầu làm lại) → người làm nhận thông báo kèm lý do.
/// </summary>
public sealed class ChangeTaskStatusHandler(IWorkDbContext db, TaskDesk desk, WorkPeople people, WorkNotices notices, IClock clock)
    : ICommandHandler<ChangeTaskStatusCommand, TaskMoveResult>
{
    public async Task<Result<TaskMoveResult>> Handle(ChangeTaskStatusCommand cmd, CancellationToken ct)
    {
        var t = await desk.FindAsync(cmd.Id, ct);
        if (t is null) return WorkTaskErrors.NotFound;
        if (!await desk.CanMoveAsync(t, ct)) return WorkTaskErrors.Forbidden;

        var before = t.Status;
        var me = people.MeId ?? Guid.Empty;
        if (!string.IsNullOrWhiteSpace(cmd.Comment) && t.AddComment(cmd.Comment, me, people.MeName, clock.UtcNow) is { IsFailure: true } bad)
            return bad.Error;
        var codes = await desk.CodesAsync(ct);
        var next = t.Move(cmd.Status, clock.UtcNow, clock.Today, codes);
        if (next is not null) db.Tasks.Add(next);

        var label = $"{t.Code} · {t.Title}";
        if (before != t.Status)
        {
            if (t.Status == WorkTaskStatus.Review && me == t.AssigneeId && t.CreatorId != me)
                notices.ToEmployees([t.CreatorId], "Task chờ bạn duyệt", $"{label} — {people.MeName}", "info", WorkNotices.TasksPage,
                    "work.task.review");
            else if (t.Status == WorkTaskStatus.Done && me != t.AssigneeId)
                notices.ToEmployees([t.AssigneeId], "Task đã được duyệt hoàn thành", $"{label} — {people.MeName}", "success", WorkNotices.MyTasksPage,
                    "work.task.done");
            else if (before == WorkTaskStatus.Done && me != t.AssigneeId)
                notices.ToEmployees([t.AssigneeId], "Yêu cầu làm lại", $"{label}{(cmd.Comment is { } c ? ": " + c.Trim() : "")} — {people.MeName}",
                    "warning", WorkNotices.MyTasksPage, "work.task.redo");
        }

        await db.SaveChangesAsync(ct);
        return new TaskMoveResult(await desk.ToDtoAsync(t, ct), next is null ? null : await desk.ToDtoAsync(next, ct));
    }
}
