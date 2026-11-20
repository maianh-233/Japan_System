using JPCenter.Api.Common.Results;
using JPCenter.Api.Common.Security;
using JPCenter.Application.Common.Messaging;
using JPCenter.Application.Common.Security;
using JPCenter.Modules.Academic.Application.Common;
using JPCenter.Modules.Academic.Application.Rooms.ChangeRoomStatus;
using JPCenter.Modules.Academic.Application.Rooms.Common;
using JPCenter.Modules.Academic.Application.Rooms.CreateRoom;
using JPCenter.Modules.Academic.Application.Rooms.GetRoomIssues;
using JPCenter.Modules.Academic.Application.Rooms.GetRoomOptions;
using JPCenter.Modules.Academic.Application.Rooms.GetRooms;
using JPCenter.Modules.Academic.Application.Rooms.HandleRoomIssue;
using JPCenter.Modules.Academic.Application.Rooms.ReportRoomIssue;
using JPCenter.Modules.Academic.Application.Rooms.UpdateRoom;
using JPCenter.Modules.Academic.Domain.Rooms;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Routing;

namespace JPCenter.Modules.Academic.Api.Endpoints;

/// <summary>Phòng học & báo hỏng — <c>/api/v1/academic/rooms</c>, quyền <c>academic.rooms.*</c></summary>
public static class RoomEndpoints
{
    public sealed record RoomBody(Guid BranchId, string Name, string? Floor, RoomType Type, int Capacity, decimal? Area, List<Equipment>? Equipment,
        string? Note, uint? RowVersion)
    {
        public RoomInput ToInput() => new(BranchId, Name, Floor, Type, Capacity, Area, Equipment ?? [], Note);
    }

    public sealed record StatusBody(RoomStatus Status, string? Reason, DateOnly? Until);

    public sealed record IssueBody(Guid RoomId, Equipment? Equipment, string Title, string? Description, IssueSeverity Severity);

    public sealed record IssueStepBody(string? Assignee, DateOnly? DueDate, string? Resolution, decimal? Cost);

    public static void Map(IEndpointRouteBuilder academic)
    {
        var g = academic.MapGroup("/rooms");
        const string page = AcademicAccess.Rooms;

        g.MapGet("/", async (Guid? branchId, ISender s, CancellationToken ct) => (await s.Send(new GetRoomsQuery(branchId), ct)).ToHttpResult())
            .RequirePermission(page, PermissionAction.View);

        // Ô chọn phòng ở form lớp / buổi — mọi nhân viên đã đăng nhập
        g.MapGet("/options", async (Guid? branchId, ISender s, CancellationToken ct) =>
                (await s.Send(new GetRoomOptionsQuery(branchId), ct)).ToHttpResult())
            .RequirePortal(Portal.Staff);

        g.MapPost("/", async (RoomBody b, ISender s, CancellationToken ct) =>
                (await s.Send(new CreateRoomCommand(b.ToInput()), ct)).ToCreatedResult(r => $"/api/v1/academic/rooms/{r.Id}"))
            .RequirePermission(page, PermissionAction.Create);

        g.MapPut("/{id:guid}", async (Guid id, RoomBody b, ISender s, CancellationToken ct) =>
                (await s.Send(new UpdateRoomCommand(id, b.ToInput(), b.RowVersion ?? 0), ct)).ToHttpResult())
            .RequirePermission(page, PermissionAction.Update);

        g.MapPost("/{id:guid}/status", async (Guid id, StatusBody b, ISender s, CancellationToken ct) =>
                (await s.Send(new ChangeRoomStatusCommand(id, b.Status, b.Reason, b.Until), ct)).ToHttpResult())
            .RequirePermission(page, PermissionAction.Update);

        g.MapGet("/issues", async (Guid? roomId, ISender s, CancellationToken ct) =>
                (await s.Send(new GetRoomIssuesQuery(roomId), ct)).ToHttpResult())
            .RequirePermission(page, PermissionAction.View);

        // Ai xem được phòng đều báo hỏng được (GV, giáo vụ, lễ tân)
        g.MapPost("/issues", async (IssueBody b, ISender s, CancellationToken ct) =>
                (await s.Send(new ReportRoomIssueCommand(b.RoomId, b.Equipment, b.Title, b.Description, b.Severity), ct))
                .ToCreatedResult(i => $"/api/v1/academic/rooms/issues/{i.Id}"))
            .RequirePermission(page, PermissionAction.View);

        foreach (var (path, step) in new[] { ("start", RoomIssueStep.Start), ("resolve", RoomIssueStep.Resolve), ("reopen", RoomIssueStep.Reopen) })
        {
            g.MapPost($"/issues/{{id:guid}}/{path}", async (Guid id, IssueStepBody? b, ISender s, CancellationToken ct) =>
                    (await s.Send(new HandleRoomIssueCommand(id, step, b?.Assignee, b?.DueDate, b?.Resolution, b?.Cost), ct)).ToHttpResult())
                .RequirePermission(page, PermissionAction.Update);
        }
    }
}
