using JPCenter.Api.Common.Security;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Routing;
using Microsoft.Extensions.DependencyInjection;

namespace JPCenter.IntegrationTests.Middleware;

/// <summary>
/// Hàng rào: MỌI endpoint phải khai báo rõ ai được gọi — RequirePermission / RequireSignedIn / RequirePortal / AllowAnonymous
/// (request-pipeline.md mục 11). Quên khai báo → test đỏ.
/// </summary>
public sealed class EndpointSecurityTests(TestApiFactory api) : IClassFixture<TestApiFactory>
{
    [Fact]
    public void Moi_endpoint_deu_khai_bao_quyen()
    {
        var endpoints = api.Services.GetRequiredService<EndpointDataSource>().Endpoints.OfType<RouteEndpoint>().ToList();
        endpoints.Count.ShouldBeGreaterThan(10);

        var missing = endpoints
            .Where(e => e.Metadata.GetMetadata<RequiredPermission>() is null
                && e.Metadata.GetMetadata<SelfServiceEndpoint>() is null
                && e.Metadata.GetMetadata<RequiredPortals>() is null
                && e.Metadata.GetMetadata<IAllowAnonymous>() is null)
            .Select(e => e.DisplayName ?? e.RoutePattern.RawText)
            .ToList();

        missing.ShouldBeEmpty($"Endpoint chưa khai báo quyền: {string.Join(", ", missing)}");
    }
}
