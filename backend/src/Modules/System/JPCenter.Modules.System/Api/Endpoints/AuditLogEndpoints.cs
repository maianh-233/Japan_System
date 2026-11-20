using JPCenter.Api.Common.Http;
using JPCenter.Api.Common.Paging;
using JPCenter.Api.Common.Results;
using JPCenter.Api.Common.Security;
using JPCenter.Application.Common.Messaging;
using JPCenter.Application.Common.Security;
using JPCenter.Modules.System.Application.AuditLogs.Common;
using JPCenter.Modules.System.Application.AuditLogs.ExportAuditLogs;
using JPCenter.Modules.System.Application.AuditLogs.GetAuditActors;
using JPCenter.Modules.System.Application.AuditLogs.GetAuditLogs;
using JPCenter.Modules.System.Application.AuditLogs.GetAuditStats;
using JPCenter.SharedKernel.Primitives;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Routing;

namespace JPCenter.Modules.System.Api.Endpoints;

/// <summary>NK-01, NK-02 — Nhật ký hệ thống (chỉ đọc). Quyền: Hệ thống › Nhật ký hệ thống; phạm vi lọc trong <see cref="AuditLogReader"/>.</summary>
public static class AuditLogEndpoints
{
    /// <summary>Tham số lọc dùng chung: <c>?branchId=&amp;category=&amp;severity=&amp;actorId=&amp;from=&amp;to=&amp;q=</c></summary>
    public sealed record FilterQuery(Guid? BranchId, QueryEnum<AuditCategory>? Category, QueryEnum<AuditSeverity>? Severity, Guid? ActorId,
        DateOnly? From, DateOnly? To, string? Q)
    {
        public AuditLogFilter ToFilter() => new(BranchId, Category?.Value, Severity?.Value, ActorId, From, To, Q);
    }

    public static void Map(IEndpointRouteBuilder system)
    {
        var group = system.MapGroup("/audit-logs");

        group.MapGet("/", async ([AsParameters] FilterQuery filter, [AsParameters] PagingRequest paging, ISender sender, CancellationToken ct) =>
                (await sender.Send(new GetAuditLogsQuery(filter.ToFilter(), paging.ToPageRequest()), ct)).ToHttpResult())
            .RequirePermission(AuditLogReader.Page, PermissionAction.View);

        group.MapGet("/stats", async (Guid? branchId, ISender sender, CancellationToken ct) =>
                (await sender.Send(new GetAuditStatsQuery(branchId), ct)).ToHttpResult())
            .RequirePermission(AuditLogReader.Page, PermissionAction.View);

        group.MapGet("/actors", async (ISender sender, CancellationToken ct) =>
                (await sender.Send(new GetAuditActorsQuery(), ct)).ToHttpResult())
            .RequirePermission(AuditLogReader.Page, PermissionAction.View);

        group.MapGet("/export", async ([AsParameters] FilterQuery filter, ISender sender, CancellationToken ct) =>
                (await sender.Send(new ExportAuditLogsCommand(filter.ToFilter()), ct)).ToHttpResult())
            .RequirePermission(AuditLogReader.Page, PermissionAction.Export);
    }
}
