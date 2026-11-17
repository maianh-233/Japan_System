using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text.Json;
using System.Text.RegularExpressions;
using JPCenter.IntegrationTests.Infrastructure;
using JPCenter.Modules.System.Domain.Users;
using JPCenter.Modules.System.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;

namespace JPCenter.IntegrationTests.System;

/// <summary>User story 01 — đăng nhập & bảo mật, chạy trên API + PostgreSQL thật với dữ liệu mẫu.</summary>
[Collection(SeededCollection.Name)]
public sealed partial class AuthFlowTests(SeededApiFactory api)
{
    private const string Pw = SeededApiFactory.Password;
    private static CancellationToken Ct => TestContext.Current.CancellationToken;

    // ---------- Hỗ trợ ----------

    /// <summary>Tài khoản demo trong README — test khác đăng nhập trực tiếp, không dùng làm tài khoản "riêng" của test.</summary>
    private static readonly string[] DemoEmails =
    [
        "superadmin@jpcenter.vn", "admin@jpcenter.vn", "quanly.caugiay@jpcenter.vn", "giaovu@jpcenter.vn", "sale@jpcenter.vn",
        "marketing@jpcenter.vn", "ketoan@jpcenter.vn", "giaovien@jpcenter.vn",
    ];

    /// <summary>Email tài khoản nhân viên thứ n (ngoài tài khoản demo) — mỗi test dùng 1 số riêng để không ảnh hưởng nhau.</summary>
    private async Task<string> StaffEmail(int n, UserAccountStatus status = UserAccountStatus.Active)
    {
        await using var scope = api.Scope();
        var db = scope.ServiceProvider.GetRequiredService<SystemDbContext>();
        return await db.UserAccounts.Where(x => x.Status == status && !DemoEmails.Contains(x.Email))
            .OrderBy(x => x.Email).Select(x => x.Email).Skip(n).FirstAsync(Ct);
    }

    /// <summary>Dữ liệu mẫu không có tài khoản "đã mời" → tạo cho nhân viên thứ n chưa có tài khoản (như PQ-08 sẽ làm).</summary>
    private async Task<string> InvitedEmail(int n)
    {
        await using var scope = api.Scope();
        var active = await scope.ServiceProvider.GetRequiredService<JPCenter.Modules.Hr.Contracts.IEmployeeDirectory>().ListActiveAsync(Ct);
        var db = scope.ServiceProvider.GetRequiredService<SystemDbContext>();
        var accounts = await db.UserAccounts.ToDictionaryAsync(x => x.EmployeeId, x => x.Status, Ct);
        // Chưa có tài khoản hoặc vẫn đang "đã mời"; bỏ nhân viên do PermissionFlowTests tự tạo
        var employee = active
            .Where(e => !e.Email.StartsWith(PermissionFlowTests.EmployeePrefix, StringComparison.Ordinal))
            .Where(e => !accounts.TryGetValue(e.Id, out var s) || s == UserAccountStatus.Invited)
            .OrderBy(e => e.FullName, StringComparer.Ordinal).ElementAt(n);
        if (!await db.UserAccounts.AnyAsync(x => x.EmployeeId == employee.Id, Ct))
        {
            db.UserAccounts.Add(UserAccount.Invite(employee.Id, employee.Email).Value);
            await db.SaveChangesAsync(Ct);
        }
        return employee.Email;
    }

    private static async Task<JsonElement> Json(HttpResponseMessage r) => JsonDocument.Parse(await r.Content.ReadAsStringAsync(Ct)).RootElement;

    private static async Task<string> Code(HttpResponseMessage r) => (await Json(r)).GetProperty("code").GetString()!;

    private Task<HttpResponseMessage> Login(HttpClient client, string id, string pw, bool remember = false) =>
        client.PostAsJsonAsync("/api/v1/system/auth/login", new { identifier = id, password = pw, remember }, Ct);

    /// <summary>Đăng nhập thành công → client có cookie refresh + header Bearer.</summary>
    private async Task<(HttpClient Client, string Token)> SignIn(string id, string pw = Pw)
    {
        var client = api.CreateClient();
        var r = await Login(client, id, pw);
        r.StatusCode.ShouldBe(HttpStatusCode.OK, await r.Content.ReadAsStringAsync(Ct));
        var token = (await Json(r)).GetProperty("accessToken").GetString()!;
        client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", token);
        return (client, token);
    }

    private HttpClient WithToken(string token)
    {
        var c = api.CreateClient();
        c.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", token);
        return c;
    }

    // ---------- AUTH-01 ----------

    [Fact]
    public async Task Sai_mat_khau_va_khong_ton_tai_cung_mot_thong_bao()
    {
        var client = api.CreateClient();
        var wrong = await Login(client, await StaffEmail(0), "SaiMatKhau123");
        var missing = await Login(client, "khong-ton-tai@jpcenter.vn", "SaiMatKhau123");

        wrong.StatusCode.ShouldBe(HttpStatusCode.Unauthorized);
        missing.StatusCode.ShouldBe(HttpStatusCode.Unauthorized);
        (await Json(wrong)).GetProperty("title").GetString().ShouldBe((await Json(missing)).GetProperty("title").GetString());
        (await Code(wrong)).ShouldBe("auth.invalid_credentials");
    }

    [Theory]
    [InlineData("superadmin@jpcenter.vn", "staff")]
    [InlineData("giaovien@jpcenter.vn", "teacher")]
    [InlineData("hocvien@jpcenter.vn", "student")]
    [InlineData("hv1000", "student")]
    [InlineData("phuhuynh@jpcenter.vn", "parent")]
    public async Task Bon_loai_tai_khoan_vao_dung_cong(string identifier, string portal)
    {
        var r = await Login(api.CreateClient(), identifier, Pw);
        r.StatusCode.ShouldBe(HttpStatusCode.OK);
        (await Json(r)).GetProperty("user").GetProperty("portal").GetString().ShouldBe(portal);
        r.Headers.GetValues("Set-Cookie").ShouldContain(c => c.StartsWith("jp_refresh=") && c.Contains("httponly"));
    }

    [Fact]
    public async Task Tai_khoan_bi_khoa_chi_bao_khi_mat_khau_dung()
    {
        var locked = await StaffEmail(0, UserAccountStatus.Locked);
        var client = api.CreateClient();
        (await Code(await Login(client, locked, "SaiMatKhau123"))).ShouldBe("auth.invalid_credentials");
        var r = await Login(client, locked, Pw);
        r.StatusCode.ShouldBe(HttpStatusCode.Forbidden);
        (await Code(r)).ShouldBe("auth.account_locked");
    }

    // ---------- AUTH-11 ----------

    [Fact]
    public async Task Sai_5_lan_thi_khoa_tam_ke_ca_khi_nhap_dung()
    {
        var email = await StaffEmail(1);
        var client = api.CreateClient();
        for (var i = 0; i < 5; i++) await Login(client, email, "SaiMatKhau123");

        var r = await Login(client, email, Pw);
        r.StatusCode.ShouldBe(HttpStatusCode.Conflict);
        (await Code(r)).ShouldBe("auth.too_many_attempts");
    }

    // ---------- AUTH-02, AUTH-03 ----------

    [Fact]
    public async Task Lam_moi_phien_xoay_vong_va_access_token_moi_dung_duoc()
    {
        var (client, _) = await SignIn(await StaffEmail(2));
        var r = await client.PostAsync("/api/v1/system/auth/refresh", null, Ct);
        r.StatusCode.ShouldBe(HttpStatusCode.OK);
        var token = (await Json(r)).GetProperty("accessToken").GetString()!;
        (await WithToken(token).GetAsync("/api/v1/system/auth/me", Ct)).StatusCode.ShouldBe(HttpStatusCode.OK);
    }

    [Fact]
    public async Task Refresh_token_cu_bi_dung_lai_thi_thu_hoi_moi_phien()
    {
        var email = await StaffEmail(3);
        var (client, token) = await SignIn(email);
        var oldCookie = await CurrentRefreshCookie(client);   // token A (sẽ bị xoay)
        (await client.PostAsync("/api/v1/system/auth/refresh", null, Ct)).StatusCode.ShouldBe(HttpStatusCode.OK); // A → B

        // Giả lập kẻ gian dùng token cũ SAU thời gian ân hạn 30 giây
        await using (var scope = api.Scope())
        {
            var db = scope.ServiceProvider.GetRequiredService<SystemDbContext>();
            await db.Database.ExecuteSqlInterpolatedAsync(
                $"update system.user_sessions set last_seen_at = last_seen_at - interval '5 minutes' where previous_token_hash is not null", Ct);
        }

        var thief = api.CreateClient();
        var stolen = new HttpRequestMessage(HttpMethod.Post, "/api/v1/system/auth/refresh");
        stolen.Headers.Add("Cookie", $"jp_refresh={oldCookie}");
        var r = await thief.SendAsync(stolen, Ct);
        (await Code(r)).ShouldBe("auth.token_reused");

        // Mọi phiên bị thu hồi: access token đang dùng của chủ tài khoản cũng hết hiệu lực ngay
        var me = await WithToken(token).GetAsync("/api/v1/system/auth/me", Ct);
        me.StatusCode.ShouldBe(HttpStatusCode.Unauthorized);
    }

    [Fact]
    public async Task Dang_xuat_thi_access_token_het_hieu_luc_ngay()
    {
        var (client, token) = await SignIn(await StaffEmail(4));
        (await client.PostAsync("/api/v1/system/auth/logout", null, Ct)).StatusCode.ShouldBe(HttpStatusCode.NoContent);

        var me = await WithToken(token).GetAsync("/api/v1/system/auth/me", Ct);
        me.StatusCode.ShouldBe(HttpStatusCode.Unauthorized);
        (await Code(me)).ShouldBe("auth.session_revoked");
        (await client.PostAsync("/api/v1/system/auth/refresh", null, Ct)).StatusCode.ShouldBe(HttpStatusCode.Unauthorized);
    }

    // ---------- AUTH-04 ----------

    [Fact]
    public async Task Quen_mat_khau_OTP_dat_lai_va_thu_hoi_phien_cu()
    {
        var email = await StaffEmail(5);
        var (_, oldToken) = await SignIn(email);
        var client = api.CreateClient();

        var send = await client.PostAsJsonAsync("/api/v1/system/auth/password/forgot", new { email }, Ct);
        send.StatusCode.ShouldBe(HttpStatusCode.OK);
        var otp = OtpRegex().Match(api.Emails.Last(email).Subject).Value;

        (await Code(await client.PostAsJsonAsync("/api/v1/system/auth/password/verify-otp", new { email, otp = otp == "000000" ? "111111" : "000000" }, Ct)))
            .ShouldBe("auth.otp_invalid");
        var verify = await client.PostAsJsonAsync("/api/v1/system/auth/password/verify-otp", new { email, otp }, Ct);
        var resetToken = (await Json(verify)).GetProperty("resetToken").GetString();

        var reset = await client.PostAsJsonAsync("/api/v1/system/auth/password/reset", new { resetToken, newPassword = "MatKhauMoi2026" }, Ct);
        reset.StatusCode.ShouldBe(HttpStatusCode.NoContent);

        (await WithToken(oldToken).GetAsync("/api/v1/system/auth/me", Ct)).StatusCode.ShouldBe(HttpStatusCode.Unauthorized);
        (await Login(client, email, "MatKhauMoi2026")).StatusCode.ShouldBe(HttpStatusCode.OK);
        api.Emails.Last(email).Subject.ShouldContain("vừa được đổi");
    }

    [Fact]
    public async Task Quen_mat_khau_email_khong_ton_tai_tra_giong_het_va_khong_gui()
    {
        var before = api.Emails.Sent.Count;
        var r = await api.CreateClient().PostAsJsonAsync("/api/v1/system/auth/password/forgot", new { email = "ai.do@khong-co.vn" }, Ct);
        r.StatusCode.ShouldBe(HttpStatusCode.OK);
        (await Json(r)).GetProperty("resendAfter").GetInt32().ShouldBe(60);
        api.Emails.Sent.Count.ShouldBe(before);
    }

    // ---------- AUTH-05 ----------

    [Fact]
    public async Task Tai_khoan_duoc_moi_bao_cho_kich_hoat_roi_kich_hoat_va_tu_dang_nhap()
    {
        var email = await InvitedEmail(0);
        var client = api.CreateClient();
        // AUTH-01.E3
        (await Code(await Login(client, email, Pw))).ShouldBe("auth.account_invited");

        // AUTH-04.E13: tài khoản chưa kích hoạt quên mật khẩu → nhận lại email mời
        await client.PostAsJsonAsync("/api/v1/system/auth/password/forgot", new { email }, Ct);
        var token = Uri.UnescapeDataString(InviteRegex().Match(api.Emails.Last(email).Html).Groups[1].Value);

        var info = await client.GetAsync($"/api/v1/system/auth/invitations/{Uri.EscapeDataString(token)}", Ct);
        info.StatusCode.ShouldBe(HttpStatusCode.OK);
        (await Json(info)).GetProperty("email").GetString().ShouldBe(email);

        var accept = await client.PostAsJsonAsync("/api/v1/system/auth/invitations/accept", new { token, password = "KichHoat2026" }, Ct);
        accept.StatusCode.ShouldBe(HttpStatusCode.OK);
        (await Code(await client.PostAsJsonAsync("/api/v1/system/auth/invitations/accept", new { token, password = "KichHoat2026" }, Ct)))
            .ShouldBe("auth.invite_invalid");
        (await Login(client, email, "KichHoat2026")).StatusCode.ShouldBe(HttpStatusCode.OK);
    }

    // ---------- AUTH-06 ----------

    [Fact]
    public async Task Doi_mat_khau_sai_mat_khau_cu_va_thanh_cong_dang_xuat_thiet_bi_khac()
    {
        var email = await StaffEmail(6);
        var (other, otherToken) = await SignIn(email);
        var (client, _) = await SignIn(email);

        (await Code(await client.PostAsJsonAsync("/api/v1/system/auth/password/change",
            new { currentPassword = "SaiMatKhau123", newPassword = "MatKhauMoi2027", logoutOtherDevices = true }, Ct))).ShouldBe("auth.wrong_password");

        var ok = await client.PostAsJsonAsync("/api/v1/system/auth/password/change",
            new { currentPassword = Pw, newPassword = "MatKhauMoi2027", logoutOtherDevices = true }, Ct);
        ok.StatusCode.ShouldBe(HttpStatusCode.OK);
        var body = await Json(ok);
        body.GetProperty("loggedOutDevices").GetInt32().ShouldBe(1);

        (await WithToken(body.GetProperty("accessToken").GetString()!).GetAsync("/api/v1/system/auth/me", Ct)).StatusCode.ShouldBe(HttpStatusCode.OK);
        (await WithToken(otherToken).GetAsync("/api/v1/system/auth/me", Ct)).StatusCode.ShouldBe(HttpStatusCode.Unauthorized);
        (await other.PostAsync("/api/v1/system/auth/refresh", null, Ct)).StatusCode.ShouldBe(HttpStatusCode.Unauthorized);
    }

    // ---------- AUTH-08, AUTH-09 ----------

    [Fact]
    public async Task Dang_xuat_tu_xa_thiet_bi_khac_va_xem_lich_su()
    {
        var email = await StaffEmail(7);
        var (_, phoneToken) = await SignIn(email);
        var (laptop, _) = await SignIn(email);

        var sessions = await Json(await laptop.GetAsync("/api/v1/system/me/sessions", Ct));
        sessions.GetArrayLength().ShouldBeGreaterThanOrEqualTo(2);
        var phone = sessions.EnumerateArray().First(s => !s.GetProperty("current").GetBoolean()).GetProperty("id").GetGuid();

        (await laptop.DeleteAsync($"/api/v1/system/me/sessions/{phone}", Ct)).StatusCode.ShouldBe(HttpStatusCode.NoContent);
        (await WithToken(phoneToken).GetAsync("/api/v1/system/auth/me", Ct)).StatusCode.ShouldBe(HttpStatusCode.Unauthorized);

        await Login(api.CreateClient(), email, "SaiMatKhau123");
        await Task.Delay(500, Ct); // nhật ký ghi nền
        var history = await Json(await laptop.GetAsync("/api/v1/system/me/login-history", Ct));
        history.GetProperty("items").EnumerateArray().ShouldContain(i => i.GetProperty("action").GetString() == "Đăng nhập thất bại");
        history.GetProperty("recentFailures").GetInt32().ShouldBe(1);
    }

    // ---------- AUTH-10 ----------

    [Fact]
    public async Task Giao_vu_cap_tai_khoan_cong_sale_khong_duoc()
    {
        var owner = Guid.NewGuid();
        var (giaovu, _) = await SignIn("giaovu@jpcenter.vn");
        var create = await giaovu.PostAsJsonAsync("/api/v1/system/portal-accounts",
            new { ownerType = "student", ownerId = owner, displayName = "Học viên Mới", username = $"hv{owner.ToString()[..6]}", email = (string?)null }, Ct);
        create.StatusCode.ShouldBe(HttpStatusCode.Created);
        var body = await Json(create);
        var temp = body.GetProperty("temporaryPassword").GetString()!;
        var username = body.GetProperty("account").GetProperty("username").GetString()!;

        var first = await Login(api.CreateClient(), username, temp);
        (await Json(first)).GetProperty("user").GetProperty("mustChangePassword").GetBoolean().ShouldBeTrue();

        (await Code(await giaovu.PostAsJsonAsync("/api/v1/system/portal-accounts",
            new { ownerType = "student", ownerId = owner, displayName = "Trùng", username = "khac123", email = (string?)null }, Ct))).ShouldBe("system.portal_account.exists");

        var (sale, _) = await SignIn("sale@jpcenter.vn");
        (await sale.PostAsJsonAsync("/api/v1/system/portal-accounts",
            new { ownerType = "student", ownerId = Guid.NewGuid(), displayName = "X", username = "xyz123", email = (string?)null }, Ct))
            .StatusCode.ShouldBe(HttpStatusCode.Forbidden);
    }

    [Fact]
    public async Task Giao_vu_khoa_mo_khoa_va_dat_lai_mat_khau_tai_khoan_cong()
    {
        var owner = Guid.NewGuid();
        var (giaovu, _) = await SignIn("giaovu@jpcenter.vn");
        var created = await Json(await giaovu.PostAsJsonAsync("/api/v1/system/portal-accounts",
            new { ownerType = "student", ownerId = owner, displayName = "Học viên Khóa", username = $"hk{owner.ToString()[..6]}", email = (string?)null }, Ct));
        var id = created.GetProperty("account").GetProperty("id").GetGuid();
        var username = created.GetProperty("account").GetProperty("username").GetString()!;
        var temp = created.GetProperty("temporaryPassword").GetString()!;

        var list = await Json(await giaovu.GetAsync($"/api/v1/system/portal-accounts?ownerType=student&ownerId={owner}", Ct));
        list.GetArrayLength().ShouldBe(1);

        // Khóa → không đăng nhập được; mở khóa → được
        var locked = await Json(await giaovu.PostAsync($"/api/v1/system/portal-accounts/{id}/lock", null, Ct));
        locked.GetProperty("status").GetString().ShouldBe("locked");
        (await Code(await Login(api.CreateClient(), username, temp))).ShouldBe("auth.account_locked");
        (await giaovu.PostAsync($"/api/v1/system/portal-accounts/{id}/unlock", null, Ct)).StatusCode.ShouldBe(HttpStatusCode.OK);

        // Đặt lại mật khẩu tạm → mật khẩu cũ hết dùng được
        var reset = await Json(await giaovu.PostAsync($"/api/v1/system/portal-accounts/{id}/reset-password", null, Ct));
        var newTemp = reset.GetProperty("temporaryPassword").GetString()!;
        newTemp.ShouldNotBe(temp);
        (await Login(api.CreateClient(), username, temp)).StatusCode.ShouldBe(HttpStatusCode.Unauthorized);
        (await Login(api.CreateClient(), username, newTemp)).StatusCode.ShouldBe(HttpStatusCode.OK);
    }

    private async Task<string> CurrentRefreshCookie(HttpClient client)
    {
        // Lấy refresh token hiện tại từ cookie của client (WebApplicationFactory giữ cookie trong handler)
        var r = await client.PostAsync("/api/v1/system/auth/refresh", null, Ct);
        r.StatusCode.ShouldBe(HttpStatusCode.OK);
        return CookieRegex().Match(string.Join(";", r.Headers.GetValues("Set-Cookie"))).Groups[1].Value;
    }

    [GeneratedRegex(@"\d{6}")]
    private static partial Regex OtpRegex();

    [GeneratedRegex(@"activate\?token=([^""&]+)")]
    private static partial Regex InviteRegex();

    [GeneratedRegex(@"jp_refresh=([^;]+)")]
    private static partial Regex CookieRegex();
}
