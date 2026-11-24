using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text;
using System.Text.Json;
using JPCenter.Infrastructure.Common.Persistence;
using JPCenter.IntegrationTests.Infrastructure;
using JPCenter.Modules.Finance.Api.Endpoints;
using JPCenter.Modules.System.Contracts;
using JPCenter.Modules.System.Contracts.Dtos;
using Microsoft.Extensions.DependencyInjection;

namespace JPCenter.IntegrationTests.System;

/// <summary>
/// User story 15 (giai đoạn 22 phase 3) — Hệ thống › Tích hợp: danh sách / che khóa, kết nối / ngắt, bật tắt việc, kiểm tra, nhật ký;
/// gửi tin Zalo → SMS / email (chế độ thử); webhook ngân hàng theo cấu hình VietQR; tạo phòng học online.
/// </summary>
[Collection(SeededCollection.Name)]
public sealed class IntegrationFlowTests(SeededApiFactory api)
{
    private const string Integrations = "/api/v1/system/integrations";
    private static CancellationToken Ct => TestContext.Current.CancellationToken;

    private async Task<HttpClient> SignIn(string email = "superadmin@jpcenter.vn")
    {
        var client = api.CreateClient();
        var r = await client.PostAsJsonAsync("/api/v1/system/auth/login",
            new { identifier = email, password = SeededApiFactory.Password, remember = false }, Ct);
        r.StatusCode.ShouldBe(HttpStatusCode.OK, await r.Content.ReadAsStringAsync(Ct));
        client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", (await Json(r)).GetProperty("accessToken").GetString());
        return client;
    }

    private static async Task<JsonElement> Json(HttpResponseMessage r) => JsonDocument.Parse(await r.Content.ReadAsStringAsync(Ct)).RootElement;

    private static async Task<JsonElement> Ok(HttpResponseMessage r)
    {
        var body = await r.Content.ReadAsStringAsync(Ct);
        r.IsSuccessStatusCode.ShouldBeTrue(body);
        return body.Length == 0 ? default : JsonDocument.Parse(body).RootElement;
    }

    private static async Task<string> Code(HttpResponseMessage r) => (await Json(r)).GetProperty("code").GetString()!;

    private static JsonElement Find(JsonElement list, string key) => list.EnumerateArray().Single(i => i.GetProperty("id").GetString() == key);

    [Fact(DisplayName = "TH-01: danh sách theo danh mục, khóa bí mật chỉ còn 4 ký tự cuối, nhãn việc, lượt dùng 30 ngày")]
    public async Task List_integrations()
    {
        var admin = await SignIn();
        var list = await Ok(await admin.GetAsync(Integrations, Ct));
        list.EnumerateArray().Select(i => i.GetProperty("id").GetString())
            .ShouldBe(["zalo-oa", "sms", "email", "vietqr", "zoom", "meet", "fb-lead", "web-form", "tiktok-lead"]);
        var zalo = Find(list, "zalo-oa");
        zalo.GetProperty("config").GetProperty("secret").GetString().ShouldBe("••••••aB4n");
        zalo.GetProperty("config").GetProperty("oaId").GetString().ShouldBe("4318826621947740");
        zalo.GetProperty("fields").EnumerateArray().Single(f => f.GetProperty("key").GetString() == "secret").GetProperty("secret").GetBoolean().ShouldBeTrue();
        zalo.GetProperty("events")[0].GetProperty("label").GetString().ShouldBe("Báo vắng học cho phụ huynh");
        zalo.GetProperty("usage").GetProperty("label").GetString().ShouldBe("tin ZNS đã gửi");
        Find(list, "email").GetProperty("events").EnumerateArray().ShouldContain(e => e.GetProperty("key").GetString() == "payslip");

        (await Code(await admin.GetAsync($"{Integrations}/khong-co/logs", Ct))).ShouldBe("system.integration.unknown");
        var sale = await SignIn("sale@jpcenter.vn");
        (await sale.GetAsync(Integrations, Ct)).StatusCode.ShouldBe(HttpStatusCode.Forbidden);
    }

    [Fact(DisplayName = "TH-02, TH-03: kết nối (thiếu trường E1, giữ khóa cũ E2, bỏ trường lạ E4), bật việc khi chưa kết nối bị chặn, ngắt xóa cấu hình")]
    public async Task Connect_and_events()
    {
        var admin = await SignIn();
        (await Code(await admin.PutAsJsonAsync($"{Integrations}/khong-co/connection", new { config = new { } }, Ct))).ShouldBe("system.integration.unknown");
        (await Code(await admin.PutAsJsonAsync($"{Integrations}/tiktok-lead/connection", new { config = new { advertiserId = "ADV1" } }, Ct)))
            .ShouldBe("system.integration.missing_field");
        (await Code(await admin.PutAsJsonAsync($"{Integrations}/tiktok-lead/events/import", new { on = true }, Ct))).ShouldBe("system.integration.not_connected");
        (await Code(await admin.PutAsJsonAsync($"{Integrations}/sms/events/khong-co", new { on = true }, Ct))).ShouldBe("system.integration.unknown_event");

        // Kết nối lại Zalo, để trống secret → giữ khóa cũ; trường lạ bị bỏ
        var zalo = await Ok(await admin.PutAsJsonAsync($"{Integrations}/zalo-oa/connection",
            new { config = new Dictionary<string, string?> { ["oaId"] = "4318826621947740", ["appId"] = "1290556631", ["secret"] = "", ["la"] = "x" }, account = "JP Center OA" }, Ct));
        zalo.GetProperty("status").GetString().ShouldBe("connected");
        zalo.GetProperty("config").GetProperty("secret").GetString().ShouldBe("••••••aB4n");
        zalo.GetProperty("config").TryGetProperty("la", out _).ShouldBeFalse();
        zalo.GetProperty("connectedBy").GetString().ShouldBe("Đỗ Quang Huy");

        var tiktok = await Ok(await admin.PutAsJsonAsync($"{Integrations}/tiktok-lead/connection",
            new { config = new { advertiserId = "ADV1", token = "tk_secret_9876" } }, Ct));
        tiktok.GetProperty("config").GetProperty("token").GetString().ShouldBe("••••••9876");
        var on = await Ok(await admin.PutAsJsonAsync($"{Integrations}/tiktok-lead/events/import", new { on = true }, Ct));
        on.GetProperty("events")[0].GetProperty("on").GetBoolean().ShouldBeTrue();

        var off = await Ok(await admin.DeleteAsync($"{Integrations}/tiktok-lead/connection", Ct));
        off.GetProperty("status").GetString().ShouldBe("disconnected");
        off.GetProperty("config").EnumerateObject().ShouldBeEmpty();
        off.GetProperty("events").EnumerateArray().ShouldAllBe(e => !e.GetProperty("on").GetBoolean());
    }

    [Fact(DisplayName = "TH-04, TH-05: kiểm tra kết nối (chế độ thử ghi \"(thử)\") → nhật ký mới nhất; chưa kết nối bị chặn")]
    public async Task Test_connection_and_logs()
    {
        var admin = await SignIn();
        var test = await Ok(await admin.PostAsync($"{Integrations}/sms/test", null, Ct));
        test.GetProperty("ok").GetBoolean().ShouldBeTrue();
        test.GetProperty("message").GetString()!.ShouldContain("(thử)");
        test.GetProperty("integration").GetProperty("lastSyncAt").ValueKind.ShouldBe(JsonValueKind.String);

        var vietqr = await Ok(await admin.PostAsync($"{Integrations}/vietqr/test", null, Ct));
        vietqr.GetProperty("message").GetString().ShouldBe("Ký thử checksum thành công");

        var logs = await Ok(await admin.GetAsync($"{Integrations}/sms/logs", Ct));
        logs[0].GetProperty("message").GetString()!.ShouldContain("(thử)");
        logs.GetArrayLength().ShouldBeLessThanOrEqualTo(50);
        (await Code(await admin.PostAsync($"{Integrations}/tiktok-lead/test", null, Ct))).ShouldBe("system.integration.not_connected");
    }

    [Fact(DisplayName = "TH-06: Zalo (thử) cho báo vắng; OTP chỉ SMS; thiếu SĐT bỏ qua; email theo việc riêng; SĐT chỉ ghi 4 số cuối")]
    public async Task Send_messages()
    {
        await using var scope = api.Scope();
        var channels = scope.ServiceProvider.GetRequiredService<IMessageChannels>();

        var zalo = await channels.SendAsync(new OutgoingMessage("attendance", "Phụ huynh A", "0912345678", null, "Báo vắng", ["Con vắng buổi 5"]), Ct);
        (zalo.Sent, zalo.Channel, zalo.Sandbox).ShouldBe((true, "zalo", true));
        zalo.Detail.ShouldContain("***5678");
        zalo.Detail.ShouldNotContain("0912345678");

        var otp = await channels.SendAsync(new OutgoingMessage("otp", null, "0987654321", null, "OTP", ["Mã 123456"]), Ct);
        otp.Channel.ShouldBe("sms");

        var noPhone = await channels.SendAsync(new OutgoingMessage("tuition", "PH B", null, "ph@x.vn", "Nhắc học phí", ["Còn nợ"]), Ct);
        noPhone.Sent.ShouldBeFalse();

        var before = api.Emails.Sent.Count;
        var mail = await channels.SendAsync(new OutgoingMessage("invoice", "PH C", null, "phc@example.com", "Hóa đơn HD-1", ["Phải thu 1.000.000₫"],
            [new("Mã", "HD-1")], "/student/tuition"), Ct);
        (mail.Sent, mail.Channel).ShouldBe((true, "email"));
        api.Emails.Sent.Skip(before).ShouldContain(m => m.To == "phc@example.com" && m.Subject == "Hóa đơn HD-1");

        var admin = await SignIn();
        var logs = await Ok(await admin.GetAsync($"{Integrations}/zalo-oa/logs", Ct));
        logs.EnumerateArray().ShouldContain(l => l.GetProperty("message").GetString()!.Contains("***5678"));
    }

    [Fact(DisplayName = "TH-07: webhook ký bằng checksum VietQR; tắt tự khớp → chờ đối soát tay; ngắt VietQR → 503; mỗi giao dịch 1 dòng nhật ký")]
    public async Task Bank_webhook_uses_vietqr_config()
    {
        var admin = await SignIn();
        async Task<HttpResponseMessage> Webhook(object body, string secret)
        {
            var raw = JsonSerializer.Serialize(body);
            var req = new HttpRequestMessage(HttpMethod.Post, "/api/v1/finance/integrations/bank/transactions")
            {
                Content = new StringContent(raw, Encoding.UTF8, "application/json"),
            };
            req.Headers.Add(ReconciliationEndpoints.SignatureHeader, "sha256=" + ReconciliationEndpoints.Sign(raw, secret));
            return await api.CreateClient().SendAsync(req, Ct);
        }
        object Txn(string reference) => new
        {
            @ref = reference, at = DateTimeOffset.UtcNow, amount = 1_000_000, content = "CK HOC PHI", payer = "NGUYEN VAN A", account = "0011004455667",
        };

        // Khóa cũ Finance:BankWebhookSecret không còn dùng khi VietQR đã cấu hình checksum
        (await Webhook(Txn($"TH07-{Guid.NewGuid():N}"[..14]), "fallback-bank-webhook-secret")).StatusCode.ShouldBe(HttpStatusCode.Unauthorized);

        await Ok(await admin.PutAsJsonAsync($"{Integrations}/vietqr/events/autoMatch", new { on = false }, Ct));
        var reference = $"TH07-{Guid.NewGuid():N}"[..14];
        var txn = await Ok(await Webhook(Txn(reference), SeededApiFactory.BankWebhookSecret));
        txn.GetProperty("status").GetString().ShouldBe("unmatched");
        var logs = await Ok(await admin.GetAsync($"{Integrations}/vietqr/logs", Ct));
        logs.EnumerateArray().ShouldContain(l => l.GetProperty("message").GetString()!.Contains(reference));
        logs.EnumerateArray().ShouldContain(l => l.GetProperty("message").GetString()!.Contains("chữ ký không hợp lệ"));
        await Ok(await admin.PutAsJsonAsync($"{Integrations}/vietqr/events/autoMatch", new { on = true }, Ct));

        // Ngắt → 503; kết nối lại cùng khóa → nhận lại bình thường
        await Ok(await admin.DeleteAsync($"{Integrations}/vietqr/connection", Ct));
        var down = await Webhook(Txn($"TH07-{Guid.NewGuid():N}"[..14]), SeededApiFactory.BankWebhookSecret);
        (down.StatusCode, await Code(down)).ShouldBe((HttpStatusCode.ServiceUnavailable, "system.integration.not_connected"));
        await Ok(await admin.PutAsJsonAsync($"{Integrations}/vietqr/connection",
            new { config = new { clientId = "jpc-2291", apiKey = "qr_live_7d1e9a", checksum = SeededApiFactory.BankWebhookSecret } }, Ct));
        await Ok(await admin.PutAsJsonAsync($"{Integrations}/vietqr/events/autoMatch", new { on = true }, Ct));
        await Ok(await admin.PutAsJsonAsync($"{Integrations}/vietqr/events/qr", new { on = true }, Ct));
        (await Webhook(Txn($"TH07-{Guid.NewGuid():N}"[..14]), SeededApiFactory.BankWebhookSecret)).StatusCode.ShouldBe(HttpStatusCode.OK);
    }

    [Fact(DisplayName = "TH-08: Zoom chưa chạy → không có phòng; kết nối Zoom + bật tự tạo → phòng định kỳ; lớp offline không tạo phòng")]
    public async Task Meeting_rooms()
    {
        await using (var scope = api.Scope())
        {
            var none = await scope.ServiceProvider.GetRequiredService<IMeetingRooms>().CreateAsync(Guid.NewGuid(), "Lớp thử", Ct);
            none.Url.ShouldBeNull();
        }

        var admin = await SignIn();
        await Ok(await admin.PutAsJsonAsync($"{Integrations}/zoom/connection", new { config = new { accountId = "aZk2-91pQ", clientId = "Zm_8812" } }, Ct));
        await Ok(await admin.PutAsJsonAsync($"{Integrations}/zoom/events/autoRoom", new { on = true }, Ct));
        await using (var scope = api.Scope())
        {
            var room = await scope.ServiceProvider.GetRequiredService<IMeetingRooms>().CreateAsync(Guid.NewGuid(), "GVV_N5-K99 · Lớp online", Ct);
            (room.Provider, room.Url!.StartsWith("https://zoom.us/j/", StringComparison.Ordinal)).ShouldBe(("zoom", true));
        }

        var offline = SeedData.IdOf("C-N5K21");
        (await Code(await admin.PostAsync($"/api/v1/academic/classes/{offline}/meeting-room", null, Ct))).ShouldBe("academic.class.not_online");
    }
}
