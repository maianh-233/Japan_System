using System.Net;
using System.Net.Http.Json;
using System.Text;
using System.Text.Json;

namespace JPCenter.IntegrationTests.Middleware;

/// <summary>request-pipeline.md mục 11 — nhập liệu hàng loạt và sai Content-Type.</summary>
public sealed class PipelineImportTests(TestApiFactory api) : IClassFixture<TestApiFactory>
{
    private static CancellationToken Ct => TestContext.Current.CancellationToken;

    [Fact]
    public async Task Nhap_5000_dong_la_1_request_khong_bi_429_gui_lan_2_cung_luc_thi_bi_tu_choi()
    {
        var client = api.ClientFor("test.items.create");
        var rows = Enumerable.Range(1, 5000).Select(i => new { name = $"Dòng {i}" }).ToArray();

        var first = client.PostAsJsonAsync("/api/v1/test/import", new { rows }, Ct);
        (await TestModule.ImportEntered.WaitAsync(TimeSpan.FromSeconds(10), Ct)).ShouldBeTrue("lần nhập 1 chưa vào xử lý");

        var second = await client.PostAsJsonAsync("/api/v1/test/import", new { rows }, Ct);
        second.StatusCode.ShouldBe(HttpStatusCode.TooManyRequests);

        TestModule.ImportRelease.Release();
        var r = await first;
        r.StatusCode.ShouldBe(HttpStatusCode.OK);
        JsonDocument.Parse(await r.Content.ReadAsStringAsync(Ct)).RootElement.GetProperty("rows").GetInt32().ShouldBe(5000);
    }

    /// <summary>
    /// Chưa đăng nhập thì nhận 401 trước (endpoint "415" nội bộ của ASP.NET không có AllowAnonymous → chính sách mặc định chặn) —
    /// không lộ thông tin cho người lạ. Đã đăng nhập mà sai Content-Type → 415.
    /// </summary>
    [Fact]
    public async Task Sai_Content_Type_thi_415()
    {
        var r = await api.ClientFor("test.items.create").PostAsync("/api/v1/test/items", new StringContent("name=An", Encoding.UTF8, "text/plain"), Ct);
        r.StatusCode.ShouldBe(HttpStatusCode.UnsupportedMediaType, await r.Content.ReadAsStringAsync(Ct));
    }
}
