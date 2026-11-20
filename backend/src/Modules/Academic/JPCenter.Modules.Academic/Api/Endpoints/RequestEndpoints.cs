using JPCenter.Api.Common.Http;
using JPCenter.Api.Common.Results;
using JPCenter.Api.Common.Security;
using JPCenter.Application.Common.Messaging;
using JPCenter.Application.Common.Security;
using JPCenter.Modules.Academic.Application.Common;
using JPCenter.Modules.Academic.Application.Requests.DecideCancelRequest;
using JPCenter.Modules.Academic.Application.Requests.DecideLeaveRequest;
using JPCenter.Modules.Academic.Application.Requests.GetCancelRequests;
using JPCenter.Modules.Academic.Application.Requests.GetInvites;
using JPCenter.Modules.Academic.Application.Requests.GetLeaveRequests;
using JPCenter.Modules.Academic.Application.Requests.GetSubstituteCandidates;
using JPCenter.Modules.Academic.Application.Requests.InviteSubstitutes;
using JPCenter.Modules.Academic.Application.Requests.WithdrawInvite;
using JPCenter.Modules.Academic.Domain.Requests;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Routing;

namespace JPCenter.Modules.Academic.Api.Endpoints;

/// <summary>
/// DT-16, DT-18 phía giáo vụ — Đơn chờ xử lý <c>/api/v1/academic/requests/{cancel|leave}</c> và mời dạy thay <c>/substitute-invites</c>,
/// quyền <c>academic.requests.*</c> (<c>approve</c> = duyệt / mời). Phạm vi chi nhánh lọc trong handler.
/// </summary>
public static class RequestEndpoints
{
    public sealed record DecideBody(string? Note);

    public sealed record InviteBody(Guid SessionId, Guid? CancelRequestId, List<Guid> TeacherIds, DateTimeOffset RespondBy, string Reason);

    public static void Map(IEndpointRouteBuilder academic)
    {
        const string page = AcademicAccess.Requests;
        var requests = academic.MapGroup("/requests");

        // ---------- Đơn hủy buổi của GV (DT-16) ----------
        requests.MapGet("/cancel", async (QueryEnum<RequestStatus>? status, Guid? branchId, ISender s, CancellationToken ct) =>
                (await s.Send(new GetCancelRequestsQuery(status?.Value, branchId), ct)).ToHttpResult())
            .RequirePermission(page, PermissionAction.View);

        requests.MapPost("/cancel/{id:guid}/approve", async (Guid id, DecideBody? b, ISender s, CancellationToken ct) =>
                (await s.Send(new DecideCancelRequestCommand(id, true, b?.Note), ct)).ToHttpResult())
            .RequirePermission(page, PermissionAction.Approve);

        requests.MapPost("/cancel/{id:guid}/reject", async (Guid id, DecideBody b, ISender s, CancellationToken ct) =>
                (await s.Send(new DecideCancelRequestCommand(id, false, b.Note), ct)).ToHttpResult())
            .RequirePermission(page, PermissionAction.Approve);

        // ---------- Đơn xin nghỉ của HV / PH (DT-18) ----------
        requests.MapGet("/leave", async (QueryEnum<RequestStatus>? status, Guid? branchId, Guid? classId, ISender s, CancellationToken ct) =>
                (await s.Send(new GetLeaveRequestsQuery(status?.Value, branchId, classId), ct)).ToHttpResult())
            .RequirePermission(page, PermissionAction.View);

        requests.MapPost("/leave/{id:guid}/approve", async (Guid id, DecideBody? b, ISender s, CancellationToken ct) =>
                (await s.Send(new DecideLeaveRequestCommand(id, true, b?.Note), ct)).ToHttpResult())
            .RequirePermission(page, PermissionAction.Approve);

        requests.MapPost("/leave/{id:guid}/reject", async (Guid id, DecideBody b, ISender s, CancellationToken ct) =>
                (await s.Send(new DecideLeaveRequestCommand(id, false, b.Note), ct)).ToHttpResult())
            .RequirePermission(page, PermissionAction.Approve);

        // ---------- Mời dạy thay (DT-16 bước 2) ----------
        var invites = academic.MapGroup("/substitute-invites");

        invites.MapGet("/", async (Guid? sessionId, QueryEnum<InviteStatus>? status, Guid? branchId, ISender s, CancellationToken ct) =>
                (await s.Send(new GetInvitesQuery(sessionId, status?.Value, branchId), ct)).ToHttpResult())
            .RequirePermission(page, PermissionAction.View);

        invites.MapGet("/candidates", async (Guid? sessionId, ISender s, CancellationToken ct) =>
                (await s.Send(new GetSubstituteCandidatesQuery(sessionId ?? Guid.Empty), ct)).ToHttpResult())
            .RequirePermission(page, PermissionAction.View);

        invites.MapPost("/", async (InviteBody b, ISender s, CancellationToken ct) =>
                (await s.Send(new InviteSubstitutesCommand(b.SessionId, b.CancelRequestId, b.TeacherIds ?? [], b.RespondBy, b.Reason), ct)).ToHttpResult())
            .RequirePermission(page, PermissionAction.Approve);

        invites.MapPost("/{id:guid}/withdraw", async (Guid id, ISender s, CancellationToken ct) =>
                (await s.Send(new WithdrawInviteCommand(id), ct)).ToHttpResult())
            .RequirePermission(page, PermissionAction.Approve);
    }
}
