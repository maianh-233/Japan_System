using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text.Json;
using System.Text.Json.Nodes;
using JPCenter.Infrastructure.Common.Persistence;
using JPCenter.Modules.Notifications.Application.Notifications.EventHandlers;
using JPCenter.Modules.Notifications.Contracts.IntegrationEvents;
using JPCenter.Modules.System.Domain.Roles;
using JPCenter.Modules.System.Infrastructure.Persistence;
using JPCenter.IntegrationTests.Infrastructure;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;

namespace JPCenter.IntegrationTests.System;

/// <summary>User story 10 — Cài đặt chung, Nhật ký hệ thống, Thùng rác, Thông báo trên API + PostgreSQL thật với dữ liệu mẫu.</summary>
[Collection(SeededCollection.Name)]
public sealed class FoundationFlowTests(SeededApiFactory api)
{
    private const string SuperAdmin = "superadmin@jpcenter.vn", Admin = "admin@jpcenter.vn", Staff = "giaovu@jpcenter.vn", Sale = "sale@jpcenter.vn";
    private const string StudentAcc = "hocvien@jpcenter.vn";
    private static CancellationToken Ct => TestContext.Current.CancellationToken;

    // ---------- Hỗ trợ ----------

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

    private static async Task<JsonElement> Ok(HttpResponseMessage r)
    {
        var body = await r.Content.ReadAsStringAsync(Ct);
        r.IsSuccessStatusCode.ShouldBeTrue(body);
        return body.Length == 0 ? default : JsonDocument.Parse(body).RootElement;
    }

    private static async Task Expect(HttpResponseMessage r, HttpStatusCode status, string? code = null)
    {
        var body = await r.Content.ReadAsStringAsync(Ct);
        r.StatusCode.ShouldBe(status, body);
        if (code is not null) JsonDocument.Parse(body).RootElement.GetProperty("code").GetString().ShouldBe(code);
    }

    private static async Task Eventually(Func<Task<bool>> condition)
    {
        for (var i = 0; i < 40; i++)
        {
            if (await condition()) return;
            await Task.Delay(250, Ct);
        }
        (await condition()).ShouldBeTrue("Hết thời gian chờ việc chạy nền");
    }

    private static string Suffix() => $"{DateTime.UtcNow:HHmmssfff}{Random.Shared.Next(100, 999)}";

    // ---------- Cài đặt chung ----------

    [Fact(DisplayName = "CD-01/02: nhân viên đọc được, HV / người không có quyền không sửa được; sửa đúng → lưu + nhật ký; sai → không lưu gì")]
    public async Task Settings_read_update_validate()
    {
        var admin = await SignIn(Admin);
        var staff = await SignIn(Staff);
        var original = await Ok(await staff.GetAsync("/api/v1/system/settings", Ct));
        var settings = JsonNode.Parse(original.GetProperty("settings").GetRawText())!.AsObject();
        settings["crm"]!["defaultSlaMinutes"]!.GetValue<int>().ShouldBe(15);
        settings["invoice"]!["resetEvery"]!.GetValue<string>().ShouldBe("month");
        settings["office"]!["from"]!.GetValue<string>().ShouldBe("08:00");

        await Expect(await (await SignIn(StudentAcc)).GetAsync("/api/v1/system/settings", Ct), HttpStatusCode.Forbidden);
        await Expect(await staff.PutAsJsonAsync("/api/v1/system/settings", new { settings }, Ct), HttpStatusCode.Forbidden);

        try
        {
            // E1: sai khoảng → lỗi theo khóa, không lưu
            var bad = settings.DeepClone().AsObject();
            bad["atRisk"]!["streak"] = 50;
            bad["crm"]!["defaultSlaMinutes"] = 30;
            var r = await admin.PutAsJsonAsync("/api/v1/system/settings", new { settings = bad }, Ct);
            await Expect(r, HttpStatusCode.BadRequest, "validation.failed");
            (await Json(r)).GetProperty("errors").TryGetProperty("atRisk.streak", out _).ShouldBeTrue();

            // E2: giờ hành chính ngược
            var hours = settings.DeepClone().AsObject();
            hours["office"]!["from"] = "18:00";
            await Expect(await admin.PutAsJsonAsync("/api/v1/system/settings", new { settings = hours }, Ct), HttpStatusCode.BadRequest,
                "system.settings.office_hours_invalid");

            // Thiếu nhóm → lỗi theo từng mục
            await Expect(await admin.PutAsJsonAsync("/api/v1/system/settings", new { settings = new { crm = new { defaultSlaMinutes = 20 } } }, Ct),
                HttpStatusCode.BadRequest, "validation.failed");

            (await Ok(await staff.GetAsync("/api/v1/system/settings", Ct))).GetProperty("settings").GetProperty("crm")
                .GetProperty("defaultSlaMinutes").GetInt32().ShouldBe(15);

            // Hợp lệ: đổi 2 mục (thứ 7 nghỉ = bỏ trống)
            var next = settings.DeepClone().AsObject();
            next["crm"]!["defaultSlaMinutes"] = 30;
            next["office"]!["satTo"] = "";
            var saved = await Ok(await admin.PutAsJsonAsync("/api/v1/system/settings", new { settings = next }, Ct));
            saved.GetProperty("changed").GetInt32().ShouldBe(2);
            saved.GetProperty("updatedBy").GetString().ShouldNotBeNullOrEmpty();
            saved.GetProperty("settings").GetProperty("office").GetProperty("satTo").ValueKind.ShouldBe(JsonValueKind.Null);

            var reread = await Ok(await staff.GetAsync("/api/v1/system/settings", Ct));
            reread.GetProperty("settings").GetProperty("crm").GetProperty("defaultSlaMinutes").GetInt32().ShouldBe(30);

            // E5: không đổi gì
            (await Ok(await admin.PutAsJsonAsync("/api/v1/system/settings", new { settings = next }, Ct))).GetProperty("changed").GetInt32().ShouldBe(0);

            // Nhật ký: thay đổi theo nhãn tiếng Việt
            await Eventually(async () =>
            {
                var logs = await Ok(await admin.GetAsync("/api/v1/system/audit-logs?category=setting&q=" + Uri.EscapeDataString("Đổi cài đặt chung"), Ct));
                return logs.GetProperty("items").EnumerateArray().Any(l => l.GetProperty("changes").EnumerateArray()
                    .Any(c => c.GetProperty("field").GetString() == "SLA liên hệ lead mặc định (phút)" && c.GetProperty("after").GetString() == "30"));
            });
        }
        finally
        {
            await Ok(await admin.PutAsJsonAsync("/api/v1/system/settings", new { settings }, Ct));
        }
    }

    // ---------- Nhật ký hệ thống ----------

    [Fact(DisplayName = "NK-01/02: lọc + phân trang, thống kê, người thực hiện, xuất; quá 1 năm bị chặn; không có quyền → 403")]
    public async Task Audit_logs_filter_page_export()
    {
        var admin = await SignIn(Admin);
        var today = DateOnly.FromDateTime(DateTime.UtcNow.AddHours(7));

        var page = await Ok(await admin.GetAsync($"/api/v1/system/audit-logs?from={today.AddYears(-1).AddDays(1):yyyy-MM-dd}&to={today:yyyy-MM-dd}&pageSize=5", Ct));
        page.GetProperty("pageSize").GetInt32().ShouldBe(5);
        page.GetProperty("total").GetInt32().ShouldBeGreaterThan(0);
        var items = page.GetProperty("items").EnumerateArray().ToList();
        items.Count.ShouldBeLessThanOrEqualTo(5);
        items.Select(i => i.GetProperty("at").GetDateTimeOffset()).ShouldBeInOrder(SortDirection.Descending);
        items[0].GetProperty("actor").GetString().ShouldNotBeNull();

        // Đăng nhập (hôm nay) có trong nhóm auth
        var auth = await Ok(await admin.GetAsync("/api/v1/system/audit-logs?category=auth&q=" + Uri.EscapeDataString("Đăng nhập"), Ct));
        auth.GetProperty("items").EnumerateArray().ShouldAllBe(i => i.GetProperty("category").GetString() == "auth");

        await Expect(await admin.GetAsync($"/api/v1/system/audit-logs?from={today.AddYears(-2):yyyy-MM-dd}&to={today:yyyy-MM-dd}", Ct),
            HttpStatusCode.BadRequest, "system.audit.range_too_long");
        await Expect(await admin.GetAsync("/api/v1/system/audit-logs?category=khong_co", Ct), HttpStatusCode.BadRequest);

        var stats = await Ok(await admin.GetAsync("/api/v1/system/audit-logs/stats", Ct));
        stats.GetProperty("today").GetInt32().ShouldBeGreaterThan(0);

        var actors = await Ok(await admin.GetAsync("/api/v1/system/audit-logs/actors", Ct));
        actors.GetArrayLength().ShouldBeGreaterThan(0);

        var export = await Ok(await admin.GetAsync("/api/v1/system/audit-logs/export?category=auth", Ct));
        export.GetProperty("items").GetArrayLength().ShouldBe(Math.Min(export.GetProperty("total").GetInt32(), 10_000));
        await Eventually(async () => (await Ok(await admin.GetAsync("/api/v1/system/audit-logs?category=export", Ct)))
            .GetProperty("items").EnumerateArray().Any(i => i.GetProperty("action").GetString() == "Xuất nhật ký hệ thống"));

        var sale = await SignIn(Sale);
        await Expect(await sale.GetAsync("/api/v1/system/audit-logs", Ct), HttpStatusCode.Forbidden);
        await Expect(await (await SignIn(Staff)).GetAsync("/api/v1/system/audit-logs/export", Ct), HttpStatusCode.Forbidden);
    }

    // ---------- Thùng rác ----------

    [Fact(DisplayName = "TR-01→03: xóa vai trò vào thùng rác; khôi phục (trùng tên → thêm hậu tố); xóa vĩnh viễn; không có quyền → 403")]
    public async Task Trash_restore_and_purge_role()
    {
        var sa = await SignIn(SuperAdmin);
        var name = "Lễ tân TR " + Suffix();

        async Task<Guid> CreateRole(string n) =>
            (await Json(await sa.PostAsJsonAsync("/api/v1/system/roles", new { name = n }, Ct))).GetProperty("role").GetProperty("id").GetGuid();

        async Task<JsonElement?> TrashOf(Guid roleId)
        {
            Guid? trashId;
            await using (var scope = api.Scope())
                trashId = await scope.ServiceProvider.GetRequiredService<SystemDbContext>().TrashItems.AsNoTracking()
                    .Where(t => t.Kind == "system.role" && t.EntityId == roleId).Select(t => (Guid?)t.Id.Value).FirstOrDefaultAsync(Ct);
            if (trashId is null) return null;
            return (await Ok(await sa.GetAsync("/api/v1/system/trash?kind=system.role", Ct))).EnumerateArray()
                .Cast<JsonElement?>().Single(t => t!.Value.GetProperty("id").GetGuid() == trashId);
        }

        // Khôi phục khi đã có role khác cùng tên
        var first = await CreateRole(name);
        await Ok(await sa.DeleteAsync($"/api/v1/system/roles/{first}", Ct));
        var item = (await TrashOf(first)).ShouldNotBeNull();
        item.GetProperty("restorable").GetBoolean().ShouldBeTrue();
        item.GetProperty("daysLeft").GetInt32().ShouldBe(30);
        item.GetProperty("deletedBy").GetString().ShouldNotBe("Hệ thống");

        var second = await CreateRole(name);
        var restored = await Ok(await sa.PostAsync($"/api/v1/system/trash/{item.GetProperty("id").GetGuid()}/restore", null, Ct));
        restored.GetProperty("note").GetString()!.ShouldContain("(khôi phục)");
        (await TrashOf(first)).ShouldBeNull();
        var roles = (await Ok(await sa.GetAsync("/api/v1/system/roles", Ct))).EnumerateArray().ToList();
        roles.ShouldContain(r => r.GetProperty("id").GetGuid() == first && r.GetProperty("name").GetString() == name + " (khôi phục)");

        // Xóa vĩnh viễn
        await Ok(await sa.DeleteAsync($"/api/v1/system/roles/{second}", Ct));
        var again = (await TrashOf(second)).ShouldNotBeNull();
        await Expect(await (await SignIn(Staff)).DeleteAsync($"/api/v1/system/trash/{again.GetProperty("id").GetGuid()}", Ct), HttpStatusCode.Forbidden);
        await Ok(await sa.DeleteAsync($"/api/v1/system/trash/{again.GetProperty("id").GetGuid()}", Ct));
        (await TrashOf(second)).ShouldBeNull();
        await Expect(await sa.PostAsync($"/api/v1/system/trash/{again.GetProperty("id").GetGuid()}/restore", null, Ct), HttpStatusCode.NotFound,
            "system.trash.not_found");

        await using var scope = api.Scope();
        var db = scope.ServiceProvider.GetRequiredService<SystemDbContext>();
        (await db.Roles.IgnoreQueryFilters([ShadowColumns.SoftDeleteFilter]).AnyAsync(r => r.Id == new RoleId(second), Ct)).ShouldBeFalse();

        await Expect(await (await SignIn(Sale)).GetAsync("/api/v1/system/trash", Ct), HttpStatusCode.Forbidden);
    }

    // ---------- Thông báo ----------

    [Fact(DisplayName = "TB-01→04: chuông của mình, đọc 1 / đọc hết, không đọc được của người khác; gửi theo quyền + chi nhánh, trừ người thao tác")]
    public async Task Notifications_inbox_and_delivery()
    {
        var staff = await SignIn(Staff);
        var student = await SignIn(StudentAcc);

        var mine = await Ok(await staff.GetAsync("/api/v1/notifications", Ct));
        var items = mine.GetProperty("items").EnumerateArray().ToList();
        items.Count.ShouldBeGreaterThan(0);
        items.Select(i => i.GetProperty("createdAt").GetDateTimeOffset()).ShouldBeInOrder(SortDirection.Descending);

        var studentItems = (await Ok(await student.GetAsync("/api/v1/notifications", Ct))).GetProperty("items").EnumerateArray().ToList();
        studentItems.ShouldNotContain(s => items.Any(i => i.GetProperty("id").GetGuid() == s.GetProperty("id").GetGuid()));

        // Gửi theo quyền duyệt đơn ở chi nhánh Cầu Giấy (giáo vụ có), trừ người thao tác (superadmin)
        var title = "Thông báo thử " + Suffix();
        await using (var scope = api.Scope())
        {
            var sa = await scope.ServiceProvider.GetRequiredService<SystemDbContext>().UserAccounts.AsNoTracking()
                .FirstAsync(a => a.Email == SuperAdmin, Ct);
            var handler = ActivatorUtilities.CreateInstance<DeliverNotification>(scope.ServiceProvider);
            await handler.Handle(new NotificationRequested(title, "Nội dung", "warning", "/admin/academic/requests", "test.notice",
                Permission: "academic.requests.approve", PermissionBranchId: SeedData.IdOf("cg"), ExcludeAccountId: sa.Id.Value), Ct);
            // Link ra ngoài → bỏ qua, không lỗi (TB-04.E2)
            await handler.Handle(new NotificationRequested(title + " xấu", null, "info", "https://example.com", "test.notice",
                Permission: "academic.requests.approve", PermissionBranchId: SeedData.IdOf("cg")), Ct);
        }

        var received = (await Ok(await staff.GetAsync("/api/v1/notifications", Ct))).GetProperty("items").EnumerateArray()
            .Where(i => i.GetProperty("title").GetString()!.StartsWith(title, StringComparison.Ordinal)).ToList();
        received.Count.ShouldBe(1);
        received[0].GetProperty("tone").GetString().ShouldBe("warning");
        received[0].GetProperty("read").GetBoolean().ShouldBeFalse();
        (await Ok(await (await SignIn(SuperAdmin)).GetAsync("/api/v1/notifications", Ct))).GetProperty("items").EnumerateArray()
            .ShouldNotContain(i => i.GetProperty("title").GetString() == title);

        // Đọc 1, người khác đọc hộ → 404
        var id = received[0].GetProperty("id").GetGuid();
        await Expect(await student.PostAsync($"/api/v1/notifications/{id}/read", null, Ct), HttpStatusCode.NotFound, "notifications.not_found");
        await Ok(await staff.PostAsync($"/api/v1/notifications/{id}/read", null, Ct));
        await Ok(await staff.PostAsync($"/api/v1/notifications/{id}/read", null, Ct)); // đọc lại: không lỗi
        var after = await Ok(await staff.GetAsync("/api/v1/notifications", Ct));
        after.GetProperty("items").EnumerateArray().Single(i => i.GetProperty("id").GetGuid() == id).GetProperty("read").GetBoolean().ShouldBeTrue();
        after.GetProperty("unread").GetInt32().ShouldBeGreaterThan(0); // thông báo mẫu chưa đọc

        // Đọc hết
        var marked = await Ok(await staff.PostAsync("/api/v1/notifications/read-all", null, Ct));
        marked.GetProperty("marked").GetInt32().ShouldBeGreaterThan(0);
        (await Ok(await staff.GetAsync("/api/v1/notifications", Ct))).GetProperty("items").EnumerateArray()
            .Where(i => i.GetProperty("title").GetString()!.StartsWith("Đơn xin hủy buổi mới", StringComparison.Ordinal) || i.GetProperty("id").GetGuid() == id)
            .ShouldAllBe(i => i.GetProperty("read").GetBoolean());

        // Chưa đăng nhập
        await Expect(await api.CreateClient().GetAsync("/api/v1/notifications", Ct), HttpStatusCode.Unauthorized);
    }
}
