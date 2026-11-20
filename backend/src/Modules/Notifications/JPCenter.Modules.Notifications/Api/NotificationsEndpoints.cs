using JPCenter.Api.Common.Results;
using JPCenter.Api.Common.Security;
using JPCenter.Application.Common.Messaging;
using JPCenter.Modules.Notifications.Application.Notifications.GetMyNotifications;
using JPCenter.Modules.Notifications.Application.Notifications.MarkAllRead;
using JPCenter.Modules.Notifications.Application.Notifications.MarkRead;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Routing;

namespace JPCenter.Modules.Notifications.Api;

/// <summary>TB-01, TB-02 — chuông thông báo trên group <c>/api/v1/notifications</c>. Mọi cổng; mỗi người chỉ thấy thông báo của mình.</summary>
public static class NotificationsEndpoints
{
    public sealed record MarkedResponse(int Marked);

    public static void Map(IEndpointRouteBuilder group)
    {
        group.MapGet("/", async (DateTimeOffset? before, int? limit, ISender sender, CancellationToken ct) =>
                (await sender.Send(new GetMyNotificationsQuery(before, limit), ct)).ToHttpResult())
            .RequireSignedIn();

        group.MapPost("/{id:guid}/read", async (Guid id, ISender sender, CancellationToken ct) =>
                (await sender.Send(new MarkReadCommand(id), ct)).ToHttpResult())
            .RequireSignedIn();

        group.MapPost("/read-all", async (ISender sender, CancellationToken ct) =>
            {
                var marked = await sender.Send(new MarkAllReadCommand(), ct);
                return marked.IsSuccess ? Results.Ok(new MarkedResponse(marked.Value)) : marked.ToHttpResult();
            })
            .RequireSignedIn();
    }
}
