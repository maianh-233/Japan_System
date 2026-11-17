using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text;
using System.Text.Json;
using System.Text.RegularExpressions;
using JPCenter.Api.Common.Security;
using JPCenter.IntegrationTests.Infrastructure;
using JPCenter.Modules.System.Infrastructure.Persistence;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Routing;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;

namespace JPCenter.IntegrationTests.System;

/// <summary>
/// Quét TỰ ĐỘNG mọi endpoint thật của API (endpoint mới thêm cũng được kiểm, khỏi viết tay từng test "sai quyền"):
/// không token → 401; endpoint cần mã quyền mà người gọi không có quyền nào → 403 <c>system.permission.forbidden</c>.
/// </summary>
[Collection(SeededCollection.Name)]
public sealed partial class EndpointAccessSweepTests(SeededApiFactory api)
{
    /// <summary>Tài khoản mẫu riêng của test này — bị gỡ hết vai trò (PQ-05.E7: vẫn đăng nhập được, không có quyền nào).</summary>
    private const string NoRoleEmail = "lan.lan2@jpcenter.vn";

    private static CancellationToken Ct => TestContext.Current.CancellationToken;

    private List<RouteEndpoint> Endpoints() => api.Services.GetRequiredService<EndpointDataSource>().Endpoints.OfType<RouteEndpoint>()
        .Where(e => e.RoutePattern.RawText!.StartsWith("/api/", StringComparison.Ordinal))
        .ToList();

    private static string Method(RouteEndpoint e) => e.Metadata.GetMetadata<HttpMethodMetadata>()!.HttpMethods[0];

    [GeneratedRegex(@"\{(?<name>[A-Za-z]+)(:(?<type>[a-z]+))?\}")]
    private static partial Regex RouteParam();

    /// <summary>Điền tham số đường dẫn bằng giá trị hợp lệ về kiểu (Guid rỗng / chuỗi) — bản ghi không tồn tại cũng không sao.</summary>
    private static string Url(RouteEndpoint e) =>
        RouteParam().Replace(e.RoutePattern.RawText!, m => m.Groups["type"].Value == "guid" ? Guid.Empty.ToString() : "x");

    private static HttpRequestMessage Request(RouteEndpoint e)
    {
        var method = Method(e);
        var req = new HttpRequestMessage(new HttpMethod(method), Url(e));
        if (method is "POST" or "PUT" or "PATCH") req.Content = new StringContent("{}", Encoding.UTF8, "application/json");
        return req;
    }

    [Fact]
    public async Task Khong_token_thi_moi_endpoint_can_dang_nhap_tra_401()
    {
        var client = api.CreateClient();
        var protectedEndpoints = Endpoints().Where(e => e.Metadata.GetMetadata<IAllowAnonymous>() is null).ToList();
        protectedEndpoints.Count.ShouldBeGreaterThan(30);

        var wrong = new List<string>();
        foreach (var e in protectedEndpoints)
        {
            var r = await client.SendAsync(Request(e), Ct);
            if (r.StatusCode != HttpStatusCode.Unauthorized) wrong.Add($"{Method(e)} {e.RoutePattern.RawText} → {(int)r.StatusCode}");
        }
        wrong.ShouldBeEmpty();
    }

    [Fact]
    public async Task Khong_co_quyen_thi_moi_endpoint_can_ma_quyen_tra_403()
    {
        await using (var scope = api.Scope())
        {
            var db = scope.ServiceProvider.GetRequiredService<SystemDbContext>();
            var account = await db.UserAccounts.FirstAsync(a => a.Email == NoRoleEmail, Ct);
            account.SetRoles([]);
            await db.SaveChangesAsync(Ct);
        }

        var client = api.CreateClient();
        var login = await client.PostAsJsonAsync("/api/v1/system/auth/login",
            new { identifier = NoRoleEmail, password = SeededApiFactory.Password, remember = false }, Ct);
        login.StatusCode.ShouldBe(HttpStatusCode.OK);
        var token = JsonDocument.Parse(await login.Content.ReadAsStringAsync(Ct)).RootElement.GetProperty("accessToken").GetString();
        client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", token);

        var guarded = Endpoints().Where(e => e.Metadata.GetMetadata<RequiredPermission>() is not null).ToList();
        guarded.Count.ShouldBeGreaterThan(20);

        var wrong = new List<string>();
        foreach (var e in guarded)
        {
            var r = await client.SendAsync(Request(e), Ct);
            var body = await r.Content.ReadAsStringAsync(Ct);
            if (r.StatusCode != HttpStatusCode.Forbidden || !body.Contains("system.permission.forbidden"))
                wrong.Add($"{Method(e)} {e.RoutePattern.RawText} → {(int)r.StatusCode} {body[..Math.Min(body.Length, 120)]}");
        }
        wrong.ShouldBeEmpty();
    }
}
