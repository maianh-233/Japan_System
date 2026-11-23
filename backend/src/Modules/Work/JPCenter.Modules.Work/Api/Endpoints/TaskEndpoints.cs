using JPCenter.Api.Common.Http;
using JPCenter.Api.Common.Results;
using JPCenter.Api.Common.Security;
using JPCenter.Application.Common.Messaging;
using JPCenter.Application.Common.Security;
using JPCenter.Modules.Work.Application.Tasks.ChangeTaskStatus;
using JPCenter.Modules.Work.Application.Tasks.Common;
using JPCenter.Modules.Work.Application.Tasks.CreateTasks;
using JPCenter.Modules.Work.Application.Tasks.DeleteTask;
using JPCenter.Modules.Work.Application.Tasks.EditTaskDetails;
using JPCenter.Modules.Work.Application.Tasks.GetTasks;
using JPCenter.Modules.Work.Application.Tasks.GetTeamReports;
using JPCenter.Modules.Work.Application.Tasks.UpdateTask;
using JPCenter.Modules.Work.Domain.Tasks;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Routing;

namespace JPCenter.Modules.Work.Api.Endpoints;

/// <summary>
/// CV-01 → CV-05. <c>/work/tasks</c> (Task toàn hệ thống: tạo cần <c>work.tasks.create</c>; xem "all" cần <c>.view</c> — kiểm trong handler;
/// sửa / trạng thái / checklist / bình luận / xóa: người dính tới task hoặc theo quyền — kiểm trong handler) ·
/// <c>/work/me/tasks</c> (Việc của tôi) · <c>/work/team/...</c> (Giao việc cấp dưới, không cần mã quyền).
/// </summary>
public static class TaskEndpoints
{
    public sealed record TeamTaskBody(IReadOnlyList<Guid> AssigneeIds, string Title, string? Description, List<Guid>? WatcherIds, TaskPriority Priority,
        DateOnly? Due, TaskRepeat Repeat, TaskLinkInput? Link, List<string>? Checklist);

    public sealed record StatusBody(WorkTaskStatus Status, string? Comment);

    public sealed record TextBody(string Text);

    public sealed record DoneBody(bool Done);

    public static void Map(IEndpointRouteBuilder work)
    {
        var g = work.MapGroup("/tasks").RequirePortal(Portal.Staff);
        g.MapGet("/", async (QueryEnum<TaskScope>? scope, Guid? assigneeId, QueryEnum<TaskPriority>? priority, QueryEnum<WorkTaskStatus>? status,
                ISender s, CancellationToken ct) =>
            (await s.Send(new GetTasksQuery(scope?.Value ?? TaskScope.All, assigneeId, priority?.Value, status?.Value), ct)).ToHttpResult());
        work.MapPost("/tasks", async (TaskInput b, ISender s, CancellationToken ct) => Created(await s.Send(new CreateTasksCommand(b, null, false), ct)))
            .RequirePermission(TaskDesk.Page, PermissionAction.Create);
        g.MapPut("/{id:guid}", async (Guid id, TaskInput b, ISender s, CancellationToken ct) =>
            (await s.Send(new UpdateTaskCommand(id, b), ct)).ToHttpResult());
        g.MapPost("/{id:guid}/status", async (Guid id, StatusBody b, ISender s, CancellationToken ct) =>
            (await s.Send(new ChangeTaskStatusCommand(id, b.Status, b.Comment), ct)).ToHttpResult());
        g.MapDelete("/{id:guid}", async (Guid id, ISender s, CancellationToken ct) => (await s.Send(new DeleteTaskCommand(id), ct)).ToHttpResult());
        g.MapPost("/{id:guid}/checklist", async (Guid id, TextBody b, ISender s, CancellationToken ct) =>
            (await s.Send(new EditTaskDetailsCommand(id, TaskDetailAction.AddItem, b.Text, null, false), ct)).ToHttpResult());
        g.MapPut("/{id:guid}/checklist/{itemId:guid}", async (Guid id, Guid itemId, DoneBody b, ISender s, CancellationToken ct) =>
            (await s.Send(new EditTaskDetailsCommand(id, TaskDetailAction.TickItem, null, itemId, b.Done), ct)).ToHttpResult());
        g.MapDelete("/{id:guid}/checklist/{itemId:guid}", async (Guid id, Guid itemId, ISender s, CancellationToken ct) =>
            (await s.Send(new EditTaskDetailsCommand(id, TaskDetailAction.RemoveItem, null, itemId, false), ct)).ToHttpResult());
        g.MapPost("/{id:guid}/comments", async (Guid id, TextBody b, ISender s, CancellationToken ct) =>
            (await s.Send(new EditTaskDetailsCommand(id, TaskDetailAction.Comment, b.Text, null, false), ct)).ToHttpResult());

        work.MapGet("/me/tasks", async (ISender s, CancellationToken ct) => (await s.Send(new GetTasksQuery(TaskScope.Mine, null, null, null), ct))
                .ToHttpResult())
            .RequirePortal(Portal.Staff);

        var team = work.MapGroup("/team").RequirePortal(Portal.Staff);
        team.MapGet("/reports", async (ISender s, CancellationToken ct) => (await s.Send(new GetTeamReportsQuery(), ct)).ToHttpResult());
        team.MapGet("/tasks", async (bool? onlyMine, Guid? assigneeId, QueryEnum<WorkTaskStatus>? status, ISender s, CancellationToken ct) =>
            (await s.Send(new GetTasksQuery(TaskScope.Team, assigneeId, null, status?.Value, onlyMine ?? false), ct)).ToHttpResult());
        team.MapPost("/tasks", async (TeamTaskBody b, ISender s, CancellationToken ct) =>
            (await s.Send(new CreateTasksCommand(new TaskInput(b.Title, b.Description, Guid.Empty, b.WatcherIds, b.Priority, b.Due, b.Repeat, b.Link,
                b.Checklist), b.AssigneeIds ?? [], true), ct)).ToHttpResult());
    }

    private static IResult Created(JPCenter.SharedKernel.Primitives.Result<IReadOnlyList<TaskDto>> r) =>
        r.IsSuccess ? Results.Created($"/api/v1/work/tasks/{r.Value[0].Id}", r.Value[0]) : r.Error.ToProblem();
}
