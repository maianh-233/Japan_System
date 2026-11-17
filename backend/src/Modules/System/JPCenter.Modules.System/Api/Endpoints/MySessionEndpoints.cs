using JPCenter.Api.Common.Results;
using JPCenter.Api.Common.Security;
using JPCenter.Application.Common.Messaging;
using JPCenter.Modules.System.Application.Sessions.GetLoginHistory;
using JPCenter.Modules.System.Application.Sessions.GetMySessions;
using JPCenter.Modules.System.Application.Sessions.RevokeOtherSessions;
using JPCenter.Modules.System.Application.Sessions.RevokeSession;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Routing;

namespace JPCenter.Modules.System.Api.Endpoints;

/// <summary>Trang Cá nhân › Mật khẩu & bảo mật — AUTH-08, AUTH-09. Chỉ cần đăng nhập, luôn là dữ liệu của chính mình.</summary>
public static class MySessionEndpoints
{
    public static void Map(IEndpointRouteBuilder system)
    {
        var me = system.MapGroup("/me").RequireSignedIn();

        me.MapGet("/sessions", async (ISender sender, CancellationToken ct) =>
            (await sender.Send(new GetMySessionsQuery(), ct)).ToHttpResult());

        me.MapDelete("/sessions/{id:guid}", async (Guid id, ISender sender, CancellationToken ct) =>
            (await sender.Send(new RevokeSessionCommand(id), ct)).ToHttpResult());

        me.MapPost("/sessions/revoke-others", async (ISender sender, CancellationToken ct) =>
            (await sender.Send(new RevokeOtherSessionsCommand(), ct)).ToHttpResult());

        me.MapGet("/login-history", async (ISender sender, CancellationToken ct) =>
            (await sender.Send(new GetLoginHistoryQuery(), ct)).ToHttpResult());
    }
}
