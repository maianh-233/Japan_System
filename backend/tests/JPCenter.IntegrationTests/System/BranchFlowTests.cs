using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text.Json;
using JPCenter.Infrastructure.Common.Outbox;
using JPCenter.Infrastructure.Common.Persistence;
using JPCenter.IntegrationTests.Infrastructure;
using JPCenter.Modules.System.Contracts.IntegrationEvents;
using JPCenter.Modules.System.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;

namespace JPCenter.IntegrationTests.System;

/// <summary>User story 03 — quản lý chi nhánh, trên API + PostgreSQL thật với dữ liệu mẫu.</summary>
[Collection(SeededCollection.Name)]
public sealed class BranchFlowTests(SeededApiFactory api)
{
    private const string Url = "/api/v1/system/branches";
    private static CancellationToken Ct => TestContext.Current.CancellationToken;
    private static int _seq;

    private async Task<HttpClient> SignIn(string email)
    {
        var client = api.CreateClient();
        var r = await client.PostAsJsonAsync("/api/v1/system/auth/login",
            new { identifier = email, password = SeededApiFactory.Password, remember = false }, Ct);
        r.StatusCode.ShouldBe(HttpStatusCode.OK, await r.Content.ReadAsStringAsync(Ct));
        client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", (await Json(r)).GetProperty("accessToken").GetString());
        return client;
    }

    private static async Task<JsonElement> Json(HttpResponseMessage r) => JsonDocument.Parse(await r.Content.ReadAsStringAsync(Ct)).RootElement;

    private static async Task<string> Code(HttpResponseMessage r) => (await Json(r)).GetProperty("code").GetString()!;

    private static async Task<JsonElement> Ok(HttpResponseMessage r)
    {
        var body = await r.Content.ReadAsStringAsync(Ct);
        r.IsSuccessStatusCode.ShouldBeTrue(body);
        return JsonDocument.Parse(body).RootElement;
    }

    /// <summary>Mã chi nhánh mới không trùng giữa các test (Z + 2 ký tự)</summary>
    private static string NewCode() => "Z" + Interlocked.Increment(ref _seq).ToString("X2");

    private static object NewBranch(string code, string? prefix = null, object? bank = null, Guid? managerId = null) => new
    {
        code, name = $"Chi nhánh {code}", address = "1 Đường Thử", phone = "0900000000", email = $"{code.ToLower()}@jpcenter.vn",
        managerId, openHours = "T2–T7 08:00–21:30", openedAt = "2027-01-01", invoicePrefix = prefix, bank, note = (string?)null,
    };

    private static object UpdateBody(JsonElement b, object? bank, string? name = null) => new
    {
        name = name ?? b.GetProperty("name").GetString(), address = b.GetProperty("address").GetString(), phone = b.GetProperty("phone").GetString(),
        email = b.GetProperty("email").GetString(), managerId = (Guid?)null, openHours = b.GetProperty("openHours").GetString(),
        openedAt = b.GetProperty("openedAt").GetString(), invoicePrefix = b.GetProperty("invoicePrefix").GetString(), bank,
        note = (string?)null, version = b.GetProperty("version").GetUInt32(),
    };

    private static async Task WaitUntil(Func<Task<bool>> condition, int seconds = 10)
    {
        var until = DateTime.UtcNow.AddSeconds(seconds);
        while (DateTime.UtcNow < until && !await condition()) await Task.Delay(100, Ct);
        (await condition()).ShouldBeTrue("Hết thời gian chờ");
    }

    // ---------- CN-01 / CN-08 ----------

    [Fact]
    public async Task Danh_sach_co_so_nhan_su_tim_khong_dau_va_loc_trang_thai()
    {
        var sa = await SignIn("superadmin@jpcenter.vn");
        var all = await Ok(await sa.GetAsync(Url, Ct));
        var cg = all.EnumerateArray().Single(b => b.GetProperty("code").GetString() == "CG");
        cg.GetProperty("stats").GetProperty("staff").GetInt32().ShouldBeGreaterThan(0);
        cg.GetProperty("stats").GetProperty("teachers").GetInt32().ShouldBeGreaterThan(0);
        cg.GetProperty("stats").TryGetProperty("rooms", out _).ShouldBeFalse(); // ⏳ chưa có module Đào tạo → không trả

        (await Ok(await sa.GetAsync($"{Url}?q=cau giay", Ct))).EnumerateArray().ShouldContain(b => b.GetProperty("code").GetString() == "CG");
        (await Ok(await sa.GetAsync($"{Url}?status=preparing", Ct))).EnumerateArray()
            .ShouldAllBe(b => b.GetProperty("status").GetString() == "preparing");

        // Sale không có quyền trang Chi nhánh nhưng vẫn lấy được danh sách cho ô chọn
        var sale = await SignIn("sale@jpcenter.vn");
        (await sale.GetAsync(Url, Ct)).StatusCode.ShouldBe(HttpStatusCode.Forbidden);
        (await Ok(await sale.GetAsync($"{Url}/options", Ct))).GetArrayLength().ShouldBeGreaterThanOrEqualTo(4);
    }

    // ---------- CN-02 ----------

    [Fact]
    public async Task Them_chi_nhanh_kiem_tra_trung_ma_tien_to_ngan_hang_quan_ly()
    {
        var sa = await SignIn("superadmin@jpcenter.vn");
        var code = NewCode();
        var created = await sa.PostAsJsonAsync(Url, NewBranch(code.ToLower()), Ct);
        created.StatusCode.ShouldBe(HttpStatusCode.Created, await created.Content.ReadAsStringAsync(Ct));
        var b = await Json(created);
        (b.GetProperty("code").GetString(), b.GetProperty("invoicePrefix").GetString(), b.GetProperty("status").GetString())
            .ShouldBe((code, code, "preparing"));

        (await Code(await sa.PostAsJsonAsync(Url, NewBranch("cg"), Ct))).ShouldBe("system.branch.duplicate_code");
        (await Code(await sa.PostAsJsonAsync(Url, NewBranch(NewCode(), prefix: "CG"), Ct))).ShouldBe("system.branch.duplicate_invoice_prefix");
        (await Code(await sa.PostAsJsonAsync(Url, NewBranch(NewCode(), bank: new { bank = "VCB", account = "123456", holder = (string?)null }), Ct)))
            .ShouldBe("system.branch.bank_incomplete");
        (await Code(await sa.PostAsJsonAsync(Url, NewBranch(NewCode(), managerId: Guid.NewGuid()), Ct))).ShouldBe("system.branch.manager_invalid");
        (await sa.PostAsJsonAsync(Url, NewBranch("C-G"), Ct)).StatusCode.ShouldBe(HttpStatusCode.BadRequest);

        // Admin chỉ có xem / sửa chi nhánh, không có thêm
        var admin = await SignIn("admin@jpcenter.vn");
        (await admin.PostAsJsonAsync(Url, NewBranch(NewCode()), Ct)).StatusCode.ShouldBe(HttpStatusCode.Forbidden);
    }

    // ---------- CN-03 / CN-04 ----------

    [Fact]
    public async Task Doi_tai_khoan_ngan_hang_bao_nguoi_phu_trach_hoa_don_va_che_so_trong_nhat_ky()
    {
        var sa = await SignIn("superadmin@jpcenter.vn");
        var b = await Json(await sa.PostAsJsonAsync(Url, NewBranch(NewCode()), Ct));
        var id = b.GetProperty("id").GetGuid();

        var updated = await Ok(await sa.PutAsJsonAsync($"{Url}/{id}",
            UpdateBody(b, new { bank = "Vietcombank", account = "0011002233", holder = "cong ty jp" }, name: "Tên mới"), Ct));
        updated.GetProperty("bank").GetProperty("holder").GetString().ShouldBe("CONG TY JP");
        updated.GetProperty("name").GetString().ShouldBe("Tên mới");

        // Admin có quyền hóa đơn mọi chi nhánh → nhận email; người sửa (superadmin) không tự nhận
        api.Emails.Sent.ShouldContain(m => m.To == "admin@jpcenter.vn" && m.Subject.Contains("Tài khoản nhận học phí") && m.Text.Contains("••••2233"));
        api.Emails.Sent.ShouldNotContain(m => m.To == "superadmin@jpcenter.vn" && m.Subject.Contains("Tài khoản nhận học phí"));

        // Nhật ký: chỉ 4 số cuối
        await WaitUntil(async () =>
        {
            await using var scope = api.Scope();
            var logs = await scope.ServiceProvider.GetRequiredService<SystemDbContext>().AuditLogs.AsNoTracking()
                .Where(a => a.Action == "Sửa chi nhánh" && a.Target == "Tên mới").ToListAsync(Ct);
            return logs.Any(a => a.Detail!.Contains("••••2233") && !a.Detail.Contains("0011002233")
                && a.Changes.All(c => c.After == null || !c.After.Contains("0011002233")));
        });

        // CN-03.E3: sửa bằng version cũ
        (await Code(await sa.PutAsJsonAsync($"{Url}/{id}", UpdateBody(b, null), Ct))).ShouldBe("system.concurrency");
    }

    // ---------- CN-05 → CN-07 ----------

    [Fact]
    public async Task Khai_truong_dong_mo_lai_va_phat_su_kien_cho_module_khac()
    {
        var sa = await SignIn("superadmin@jpcenter.vn");
        var code = NewCode();
        var id = (await Json(await sa.PostAsJsonAsync(Url, NewBranch(code), Ct))).GetProperty("id").GetGuid();

        var opened = await Ok(await sa.PostAsync($"{Url}/{id}/open", null, Ct));
        opened.GetProperty("branch").GetProperty("status").GetString().ShouldBe("active");
        opened.GetProperty("branch").GetProperty("openedAt").GetString().ShouldBe(DateOnly.FromDateTime(DateTime.UtcNow.AddHours(7)).ToString("yyyy-MM-dd"));
        opened.GetProperty("warnings").GetArrayLength().ShouldBe(2); // chưa có ngân hàng, chưa có quản lý
        (await Code(await sa.PostAsync($"{Url}/{id}/open", null, Ct))).ShouldBe("system.branch.invalid_status");

        (await Code(await sa.PostAsJsonAsync($"{Url}/{id}/close", new { confirmCode = "SAI" }, Ct))).ShouldBe("system.branch.confirm_mismatch");
        (await Ok(await sa.PostAsJsonAsync($"{Url}/{id}/close", new { confirmCode = code.ToLower() }, Ct)))
            .GetProperty("branch").GetProperty("status").GetString().ShouldBe("closed");
        (await Ok(await sa.PostAsync($"{Url}/{id}/reopen", null, Ct))).GetProperty("branch").GetProperty("status").GetString().ShouldBe("active");

        // Mỗi lần đổi trạng thái → 1 sự kiện BranchStatusChanged trong outbox, được gửi đi
        await WaitUntil(async () =>
        {
            await using var scope = api.Scope();
            var events = await scope.ServiceProvider.GetRequiredService<SystemDbContext>().Set<OutboxMessage>().AsNoTracking()
                .Where(m => m.Type == typeof(BranchStatusChanged).FullName).ToListAsync(Ct);
            var mine = events.Where(m => m.Payload.Contains(id.ToString())).ToList();
            return mine.Count == 3 && mine.All(m => m.ProcessedAt != null);
        });

        // CN-06.E2: Cầu Giấy còn nhân sự
        var cg = SeedData.IdOf("cg");
        (await Code(await sa.PostAsJsonAsync($"{Url}/{cg}/close", new { confirmCode = "CG" }, Ct))).ShouldBe("system.branch.has_staff");
    }
}
