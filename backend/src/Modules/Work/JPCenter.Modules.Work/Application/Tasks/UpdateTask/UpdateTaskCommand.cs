using FluentValidation;
using JPCenter.Application.Common.Messaging;
using JPCenter.Application.Common.Security;
using JPCenter.Modules.Work.Application.Abstractions;
using JPCenter.Modules.Work.Application.Common;
using JPCenter.Modules.Work.Application.Tasks.Common;
using JPCenter.Modules.Work.Application.Tasks.CreateTasks;
using JPCenter.Modules.Work.Domain.Tasks;
using JPCenter.SharedKernel.Primitives;

namespace JPCenter.Modules.Work.Application.Tasks.UpdateTask;

/// <summary>
/// CV-01 — sửa task (người giao / <c>work.tasks.update</c> / quản lý người làm). Đổi người làm (E5) → chi nhánh theo người mới, người mới nhận thông báo.
/// </summary>
public sealed record UpdateTaskCommand(Guid Id, TaskInput Input) : ICommand<TaskDto>;

public sealed class UpdateTaskValidator : AbstractValidator<UpdateTaskCommand>
{
    public UpdateTaskValidator() => RuleFor(x => x.Input).NotNull().SetValidator(new TaskInputValidator());
}

public sealed class UpdateTaskHandler(IWorkDbContext db, TaskDesk desk, WorkPeople people, WorkNotices notices)
    : ICommandHandler<UpdateTaskCommand, TaskDto>
{
    public async Task<Result<TaskDto>> Handle(UpdateTaskCommand cmd, CancellationToken ct)
    {
        var t = await desk.FindAsync(cmd.Id, ct);
        if (t is null) return WorkTaskErrors.NotFound;
        if (!await desk.CanEditAsync(t, ct)) return WorkTaskErrors.Forbidden;
        var i = cmd.Input;

        if (i.AssigneeId != t.AssigneeId)
        {
            var assignee = await desk.AssigneeAsync(i.AssigneeId, PermissionAction.Update, reportsOnly: false, ct);
            if (assignee is null) return WorkTaskErrors.AssigneeInvalid;
            t.Reassign(assignee.Id, new BranchId(assignee.BranchId));
            if (assignee.Id != people.MeId)
                notices.ToEmployees([assignee.Id], "Bạn được giao việc", $"{t.Code} · {t.Title}{CreateTasksHandler.Due(t)} — {people.MeName}", "info",
                    WorkNotices.MyTasksPage, "work.task.assigned");
        }
        var updated = t.Update(i.Title, i.Description, i.Priority, i.Due, i.Repeat, i.WatcherIds ?? []);
        if (updated.IsFailure) return updated.Error;

        if (i.Link is { } link)
        {
            if (link.Type != t.LinkType || link.Id != t.LinkId)
            {
                var label = await desk.LinkLabelAsync(link, ct);
                if (label is null) return WorkTaskErrors.LinkNotFound;
                t.LinkTo(link.Type, link.Id, label);
            }
        }
        else t.Unlink();

        await db.SaveChangesAsync(ct);
        return await desk.ToDtoAsync(t, ct);
    }
}
