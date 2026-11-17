using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text;
using System.Text.Json;
using JPCenter.Api.Common.Security;
using JPCenter.SharedKernel.Primitives;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;
using Microsoft.IdentityModel.JsonWebTokens;
using Microsoft.IdentityModel.Tokens;

namespace JPCenter.IntegrationTests.Middleware;

/// <summary>Kiểm tra docs/request-pipeline.md mục 11 trên API chạy thật.</summary>
public sealed class RequestPipelineTests(TestApiFactory api) : IClassFixture<TestApiFactory>
{
    private static CancellationToken Ct => TestContext.Current.CancellationToken;

    private static async Task<JsonElement> Problem(HttpResponseMessage r) =>
        JsonDocument.Parse(await r.Content.ReadAsStringAsync(Ct)).RootElement;

    private static async Task<string> Code(HttpResponseMessage r) => (await Problem(r)).GetProperty("code").GetString()!;

    // ---------- Token ----------

    [Fact]
    public async Task Khong_co_token_thi_401()
    {
        var r = await api.CreateClient().GetAsync("/api/v1/test/items", Ct);
        r.StatusCode.ShouldBe(HttpStatusCode.Unauthorized);
        (await Code(r)).ShouldBe("auth.unauthenticated");
    }

    [Fact]
    public async Task Token_sai_chu_ky_thi_401()
    {
        var client = api.CreateClient();
        client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", MakeToken(Guid.NewGuid(), "khoa-khac-hoan-toan-0123456789-abcdefghijk", DateTime.UtcNow.AddMinutes(5)));
        (await client.GetAsync("/api/v1/test/items", Ct)).StatusCode.ShouldBe(HttpStatusCode.Unauthorized);
    }

    [Fact]
    public async Task Token_het_han_thi_401_ma_token_expired_de_frontend_lam_moi()
    {
        var client = api.CreateClient();
        client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", MakeToken(Guid.NewGuid(), TestApiFactory.SigningKey, DateTime.UtcNow.AddMinutes(-5)));
        var r = await client.GetAsync("/api/v1/test/items", Ct);
        r.StatusCode.ShouldBe(HttpStatusCode.Unauthorized);
        (await Code(r)).ShouldBe("auth.token_expired");
    }

    /// <summary>Đổi mật khẩu / bị khóa / đổi quyền → tăng token_version + xóa cache → token cũ hết hiệu lực NGAY.</summary>
    [Fact]
    public async Task Doi_token_version_thi_token_cu_het_hieu_luc()
    {
        var access = api.Accounts.Add("test.items.view");
        var (token, _) = api.Services.GetRequiredService<AccessTokenIssuer>().Issue(access.AccountId, access.Portal, access.TokenVersion, Guid.NewGuid());
        var client = api.CreateClient();
        client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", token);
        (await client.GetAsync("/api/v1/test/items", Ct)).StatusCode.ShouldBe(HttpStatusCode.OK);

        api.Accounts.Update(access.AccountId, a => a with { TokenVersion = a.TokenVersion + 1 });
        api.Services.GetRequiredService<JPCenter.Application.Common.Security.IUserAccessCache>().Invalidate(access.AccountId);

        var r = await client.GetAsync("/api/v1/test/items", Ct);
        r.StatusCode.ShouldBe(HttpStatusCode.Unauthorized);
        (await Code(r)).ShouldBe("auth.token_revoked");
    }

    // ---------- Quyền ----------

    [Fact]
    public async Task Thieu_quyen_thi_403_va_ghi_nhat_ky_canh_bao()
    {
        var client = api.ClientFor("test.items.view");
        var r = await client.PostAsJsonAsync("/api/v1/test/sensitive", new { }, Ct);
        r.StatusCode.ShouldBe(HttpStatusCode.Forbidden);
        (await Code(r)).ShouldBe("system.permission.forbidden");

        var entry = await api.Audit.WaitForAsync(e => e.Action == "Từ chối truy cập" && e.Detail!.Contains("test.items.update"));
        entry.Category.ShouldBe(AuditCategory.Permission);
        entry.Severity.ShouldBe(AuditSeverity.Warning);
    }

    [Fact]
    public async Task Co_quyen_thi_200()
    {
        var r = await api.ClientFor("test.items.view").GetAsync("/api/v1/test/items", Ct);
        r.StatusCode.ShouldBe(HttpStatusCode.OK);
    }

    [Fact]
    public async Task Trang_ca_nhan_chi_can_dang_nhap()
    {
        var r = await api.ClientFor().GetAsync("/api/v1/test/me", Ct);
        r.StatusCode.ShouldBe(HttpStatusCode.OK);
    }

    // ---------- Rate limit ----------

    [Fact]
    public async Task Thao_tac_nhay_cam_gui_qua_3_lan_trong_10_giay_thi_429_kem_Retry_After()
    {
        var client = api.ClientFor("test.items.update");
        for (var i = 0; i < 3; i++)
            (await client.PostAsync("/api/v1/test/sensitive", null, Ct)).StatusCode.ShouldBe(HttpStatusCode.NoContent);

        var r = await client.PostAsync("/api/v1/test/sensitive", null, Ct);
        r.StatusCode.ShouldBe(HttpStatusCode.TooManyRequests);
        r.Headers.RetryAfter.ShouldNotBeNull();
        var p = await Problem(r);
        p.GetProperty("code").GetString().ShouldBe("system.too_many_requests");
        p.GetProperty("title").GetString()!.ShouldContain("không bấm liên tục");
    }

    [Fact]
    public async Task Spam_lien_tuc_nhieu_lan_thi_bi_tam_khoa_thao_tac_ghi_nhung_van_xem_duoc()
    {
        var client = api.ClientFor("test.items.update", "test.items.view");
        for (var i = 0; i < 6; i++) await client.PostAsync("/api/v1/test/sensitive", null, Ct); // 3 lần vượt ngưỡng

        var write = await client.PostAsync("/api/v1/test/sensitive", null, Ct);
        write.StatusCode.ShouldBe(HttpStatusCode.TooManyRequests);
        (await Code(write)).ShouldBe("system.temporarily_blocked");
        (await client.GetAsync("/api/v1/test/items", Ct)).StatusCode.ShouldBe(HttpStatusCode.OK);

        var entry = await api.Audit.WaitForAsync(e => e.Category == AuditCategory.Security);
        entry.Severity.ShouldBe(AuditSeverity.Warning);
    }

    // ---------- Chống gửi trùng ----------

    [Fact]
    public async Task Cung_Idempotency_Key_chi_tao_1_lan_va_tra_lai_response_cu()
    {
        var client = api.ClientFor("test.items.create");
        var key = Guid.NewGuid().ToString("N");
        var before = TestModule.CreateCalls;

        var first = await Send(client, key, new { name = "Lớp N3" });
        var second = await Send(client, key, new { name = "Lớp N3" });

        first.StatusCode.ShouldBe(HttpStatusCode.Created);
        second.StatusCode.ShouldBe(HttpStatusCode.Created);
        (await second.Content.ReadAsStringAsync(Ct)).ShouldBe(await first.Content.ReadAsStringAsync(Ct));
        second.Headers.GetValues("Idempotent-Replayed").ShouldContain("true");
        (TestModule.CreateCalls - before).ShouldBe(1);
    }

    [Fact]
    public async Task Cung_key_khac_noi_dung_thi_422()
    {
        var client = api.ClientFor("test.items.create");
        var key = Guid.NewGuid().ToString("N");
        await Send(client, key, new { name = "A" });
        var r = await Send(client, key, new { name = "B" });
        r.StatusCode.ShouldBe(HttpStatusCode.UnprocessableEntity);
        (await Code(r)).ShouldBe("system.idempotency_mismatch");
    }

    [Fact]
    public async Task Thieu_Idempotency_Key_thi_400()
    {
        var r = await api.ClientFor("test.items.create").PostAsJsonAsync("/api/v1/test/items", new { name = "A" }, Ct);
        r.StatusCode.ShouldBe(HttpStatusCode.BadRequest);
        (await Code(r)).ShouldBe("system.idempotency_key_required");
    }

    // ---------- Audit ----------

    [Fact]
    public async Task Thao_tac_thanh_cong_co_dong_nhat_ky_kem_nguoi_thao_tac()
    {
        var client = api.ClientFor("test.items.create");
        var r = await Send(client, Guid.NewGuid().ToString("N"), new { name = "Ghi nhật ký" });
        r.StatusCode.ShouldBe(HttpStatusCode.Created);

        var entry = await api.Audit.WaitForAsync(e => e.Action == "Tạo mục thử" && e.TraceId == r.Headers.GetValues("X-Request-Id").Single());
        entry.Category.ShouldBe(AuditCategory.Data);
        entry.ActorName.ShouldStartWith("Người thử");
        entry.ActorRole.ShouldBe("Giáo vụ");
    }

    // ---------- Dữ liệu vào ----------

    [Fact]
    public async Task Gui_truong_la_thi_400_chong_mass_assignment()
    {
        var r = await api.CreateClient().PostAsync("/api/v1/test/echo",
            new StringContent("""{"name":"A","isAdmin":true}""", Encoding.UTF8, "application/json"), Ct);
        r.StatusCode.ShouldBe(HttpStatusCode.BadRequest);
        (await Code(r)).ShouldBe("validation.malformed_json");
    }

    [Fact]
    public async Task Chuoi_duoc_chuan_hoa_NFC_va_bo_ky_tu_dieu_khien()
    {
        var decomposed = "Nguyễn\u0007"; // "Nguyễn" dạng tổ hợp + ký tự chuông
        var r = await api.CreateClient().PostAsJsonAsync("/api/v1/test/echo", new { name = decomposed }, Ct);
        var name = (await r.Content.ReadFromJsonAsync<JsonElement>(Ct)).GetProperty("name").GetString();
        name.ShouldBe("Nguyễn");
    }

    [Fact]
    public async Task Body_qua_1MB_thi_413()
    {
        var big = new string('a', 1_100_000);
        var r = await api.CreateClient().PostAsJsonAsync("/api/v1/test/echo", new { name = big }, Ct);
        r.StatusCode.ShouldBe(HttpStatusCode.RequestEntityTooLarge);
    }

    // ---------- Lỗi, truy vết, header ----------

    [Fact]
    public async Task Loi_he_thong_tra_500_khong_lo_chi_tiet_va_co_ma_tra_cuu()
    {
        var r = await api.CreateClient().GetAsync("/api/v1/test/boom", Ct);
        r.StatusCode.ShouldBe(HttpStatusCode.InternalServerError);
        var text = await r.Content.ReadAsStringAsync(Ct);
        text.ShouldNotContain("bí mật nội bộ");
        var p = JsonDocument.Parse(text).RootElement;
        p.GetProperty("code").GetString().ShouldBe("system.unexpected");
        p.GetProperty("traceId").GetString().ShouldBe(r.Headers.GetValues("X-Request-Id").Single());
    }

    [Fact]
    public async Task Ma_truy_vet_cua_client_duoc_giu_nguyen()
    {
        var client = api.CreateClient();
        client.DefaultRequestHeaders.Add("X-Request-Id", "fe-1234567890");
        var r = await client.GetAsync("/api/v1/test/open", Ct);
        r.Headers.GetValues("X-Request-Id").Single().ShouldBe("fe-1234567890");
    }

    [Fact]
    public async Task Co_header_bao_mat_va_khong_cache()
    {
        var r = await api.CreateClient().GetAsync("/api/v1/test/open", Ct);
        r.Headers.GetValues("X-Content-Type-Options").ShouldContain("nosniff");
        r.Headers.GetValues("X-Frame-Options").ShouldContain("DENY");
        r.Headers.CacheControl!.NoStore.ShouldBeTrue();
    }

    [Fact]
    public async Task CORS_chi_cho_domain_frontend()
    {
        var ok = new HttpRequestMessage(HttpMethod.Options, "/api/v1/test/open");
        ok.Headers.Add("Origin", "http://localhost:5173");
        ok.Headers.Add("Access-Control-Request-Method", "GET");
        var r1 = await api.CreateClient().SendAsync(ok, Ct);
        r1.Headers.GetValues("Access-Control-Allow-Origin").ShouldContain("http://localhost:5173");

        var bad = new HttpRequestMessage(HttpMethod.Options, "/api/v1/test/open");
        bad.Headers.Add("Origin", "https://evil.example");
        bad.Headers.Add("Access-Control-Request-Method", "GET");
        var r2 = await api.CreateClient().SendAsync(bad, Ct);
        r2.Headers.Contains("Access-Control-Allow-Origin").ShouldBeFalse();
    }

    private static Task<HttpResponseMessage> Send(HttpClient client, string key, object body)
    {
        var req = new HttpRequestMessage(HttpMethod.Post, "/api/v1/test/items") { Content = JsonContent.Create(body) };
        req.Headers.Add("Idempotency-Key", key);
        return client.SendAsync(req, Ct);
    }

    private static string MakeToken(Guid accountId, string key, DateTime expires) => new JsonWebTokenHandler().CreateToken(new SecurityTokenDescriptor
    {
        Issuer = "jpcenter-api",
        Audience = "jpcenter-web",
        NotBefore = expires.AddMinutes(-20),
        IssuedAt = expires.AddMinutes(-20),
        Expires = expires,
        Claims = new Dictionary<string, object> { ["sub"] = accountId.ToString(), ["portal"] = "staff", ["tv"] = "1" },
        SigningCredentials = new SigningCredentials(new SymmetricSecurityKey(Encoding.UTF8.GetBytes(key)), SecurityAlgorithms.HmacSha256),
    });
}
