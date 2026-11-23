using FluentValidation;
using JPCenter.Application.Common.Messaging;
using JPCenter.Application.Common.Security;
using JPCenter.Modules.Work.Application.Abstractions;
using JPCenter.Modules.Work.Application.Common;
using JPCenter.Modules.Work.Application.Tasks.Common;
using JPCenter.Modules.Work.Domain.Tasks;
using JPCenter.SharedKernel.Primitives;

namespace JPCenter.Modules.Work.Application.Tasks.CreateTasks;

/// <summary>
/// CV-01 — giao task (trang Task toàn hệ thống, <c>work.tasks.create</c>: người làm trong phạm vi) ·
/// CV-04 — giao việc cấp dưới (<paramref name="TeamOnly"/>, không cần mã quyền: người làm phải là cấp dưới; nhiều người → mỗi người 1 task riêng).
/// <c>Input.AssigneeId</c> bị bỏ qua khi có <paramref name="AssigneeIds"/>.
/// </summary>
public sealed record CreateTasksCommand(TaskInput Input, IReadOnlyList<Guid>? AssigneeIds, bool TeamOnly) : ICommand<IReadOnlyList<TaskDto>>;

public sealed class CreateTasksValidator : AbstractValidator<CreateTasksCommand>
{
    public CreateTasksValidator()
    {
        RuleFor(x => x.Input).NotNull().SetValidator(new TaskInputValidator());
        RuleFor(x => x.AssigneeIds).Must(a => a is null || a.Count is > 0 and <= 50).WithMessage("Chọn 1–50 người làm");
    }
}

public sealed class CreateTasksHandler(IWorkDbContext db, TaskDesk desk, WorkPeople people, WorkNotices notices)
    : ICommandHandler<CreateTasksCommand, IReadOnlyList<TaskDto>>
{
    public static readonly Error NoEmployee = Error.Forbidden("work.task.no_employee", "Tài khoản của bạn chưa gắn hồ sơ nhân sự");

    public async Task<Result<IReadOnlyList<TaskDto>>> Handle(CreateTasksCommand cmd, CancellationToken ct)
    {
        if (people.MeId is not { } me) return NoEmployee;
        var i = cmd.Input;
        var assigneeIds = (cmd.AssigneeIds is { Count: > 0 } many ? many : [i.AssigneeId]).Distinct().ToList();

        string? label = null;
        if (i.Link is { } link && (label = await desk.LinkLabelAsync(link, ct)) is null) return WorkTaskErrors.LinkNotFound;

        var codes = await desk.CodesAsync(ct);
        var created = new List<WorkTask>();
        foreach (var id in assigneeIds)
        {
            var assignee = await desk.AssigneeAsync(id, PermissionAction.Create, cmd.TeamOnly, ct);
            if (assignee is null) return cmd.TeamOnly ? WorkTaskErrors.NotYourReport : WorkTaskErrors.AssigneeInvalid;
            var task = WorkTask.Create(codes(), i.Title, i.Description, new BranchId(assignee.BranchId), assignee.Id, me, i.WatcherIds ?? [], i.Priority,
                i.Due, i.Repeat, i.Checklist);
            if (task.IsFailure) return task.Error;
            if (i.Link is { } l) task.Value.LinkTo(l.Type, l.Id, label!);
            db.Tasks.Add(task.Value);
            created.Add(task.Value);
            if (assignee.Id != me)
                notices.ToEmployees([assignee.Id], "Bạn được giao việc", $"{task.Value.Code} · {task.Value.Title}{Due(task.Value)} — {people.MeName}",
                    "info", WorkNotices.MyTasksPage, "work.task.assigned");
        }

        await db.SaveChangesAsync(ct);
        return Result.Success(await desk.ToDtosAsync(created, ct));
    }

    public static string Due(WorkTask t) => t.Due is { } d ? $" (hạn {d:dd/MM})" : "";
}
