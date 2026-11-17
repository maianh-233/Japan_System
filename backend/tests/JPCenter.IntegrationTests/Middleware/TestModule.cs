using JPCenter.Api.Common.Auditing;
using JPCenter.Api.Common.Idempotency;
using JPCenter.Api.Common.Modules;
using JPCenter.Api.Common.RateLimiting;
using JPCenter.Api.Common.Security;
using JPCenter.Application.Common.Security;
using JPCenter.SharedKernel.Primitives;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Routing;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;

namespace JPCenter.IntegrationTests.Middleware;

/// <summary>Module chỉ có trong test — endpoint mẫu để kiểm tra từng lớp middleware.</summary>
public sealed class TestModule : IModule
{
    public static int CreateCalls;

    public string Name => "test";

    public void AddServices(IServiceCollection services, IConfiguration configuration) { }

    public void MapEndpoints(IEndpointRouteBuilder group)
    {
        group.MapGet("/open", () => Results.Ok(new { ok = true })).AllowAnonymous();

        group.MapPost("/echo", (EchoRequest body) => Results.Ok(body)).AllowAnonymous();

        group.MapGet("/boom", IResult () => throw new InvalidOperationException("bí mật nội bộ")).AllowAnonymous();

        group.MapGet("/me", (ICurrentUser user) => Results.Ok(new { name = user.Access!.DisplayName })).RequireSignedIn();

        group.MapGet("/items", () => Results.Ok(new[] { "a", "b" })).RequirePermission("test.items", PermissionAction.View);

        group.MapPost("/items", (CreateItemRequest body) =>
            {
                var n = Interlocked.Increment(ref CreateCalls);
                return Results.Created($"/api/v1/test/items/{n}", new { id = n, name = body.Name });
            })
            .RequirePermission("test.items", PermissionAction.Create)
            .Idempotent()
            .Audited("Tạo mục thử", AuditCategory.Data);

        group.MapPost("/sensitive", () => Results.NoContent())
            .RequirePermission("test.items", PermissionAction.Update)
            .RequireRateLimiting(RateLimitPolicies.WriteSensitive);
    }
}

public sealed record EchoRequest(string Name);

public sealed record CreateItemRequest(string Name);
