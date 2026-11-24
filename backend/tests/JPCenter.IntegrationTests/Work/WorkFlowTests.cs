using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text.Json;
using JPCenter.Application.Common.Messaging;
using JPCenter.Infrastructure.Common.Persistence;
using JPCenter.IntegrationTests.Infrastructure;
using JPCenter.Modules.Work.Application.Tasks.RemindDueTasks;
using JPCenter.Modules.Work.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;

namespace JPCenter.IntegrationTests.Work;

/// <summary>User story 14 — task, giao việc cấp dưới, xếp ca, bàn giao ca trên API + PostgreSQL thật với dữ liệu mẫu.</summary>
[Collection(SeededCollection.Name)]
public sealed class WorkFlowTests(SeededApiFactory api)
{
    private const string Admin = "admin@jpcenter.vn", Manager = "quanly.caugiay@jpcenter.vn", Tung = "tung.tung@jpcenter.vn",
        Hang = "hang.hang@jpcenter.vn", Sale = "sale@jpcenter.vn";
    private static CancellationToken Ct => TestContext.Current.CancellationToken;
    private static readonly Guid Cg = SeedData.IdOf("cg");
    private static readonly Guid ETung = SeedData.IdOf("e-tung");
    private static readonly Guid EHang = SeedData.IdOf("e-hang");
    private static readonly Guid EBao = SeedData.IdOf("e-bao");
    private static readonly Guid EKhoa = SeedData.IdOf("e-khoa");
    private static DateOnly Today => DateOnly.FromDateTime(DateTime.UtcNow.AddHours(7));
    private static int _seq;

    private async Task<HttpClient> SignIn(string email)
    {
        var client = api.CreateClient();
        var r = await client.PostAsJsonAsync("/api/v1/system/auth/login", new { identifier = email, password = SeededApiFactory.Password, remember = false }, Ct);
        r.StatusCode.ShouldBe(HttpStatusCode.OK, await r.Content.ReadAsStringAsync(Ct));
        client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer",
            JsonDocument.Parse(await r.Content.ReadAsStringAsync(Ct)).RootElement.GetProperty("accessToken").GetString());
        return client;
    }

    private static async Task<JsonElement> Ok(HttpResponseMessage r)
    {
        var body = await r.Content.ReadAsStringAsync(Ct);
        r.IsSuccessStatusCode.ShouldBeTrue(body);
        return body.Length == 0 ? default : JsonDocument.Parse(body).RootElement;
    }

    private static async Task Fails(HttpResponseMessage r, HttpStatusCode status, string code)
    {
        var body = await r.Content.ReadAsStringAsync(Ct);
        r.StatusCode.ShouldBe(status, body);
        body.ShouldContain($"\"{code}\"", Case.Sensitive, body);
    }

    private static string D(DateOnly d) => d.ToString("yyyy-MM-dd");

    private static object NewTask(Guid assignee, string repeat = "none", DateOnly? due = null) => new
    {
        title = $"Việc thử {Interlocked.Increment(ref _seq)}", description = (string?)null, assigneeId = assignee, watcherIds = Array.Empty<Guid>(),
        priority = "high", due = due is { } d ? D(d) : null, repeat, link = (object?)null, checklist = new[] { "Bước 1", "Bước 2" },
    };

    private static async Task WaitUntil(Func<Task<bool>> condition, int seconds = 15)
    {
        var until = DateTime.UtcNow.AddSeconds(seconds);
        while (DateTime.UtcNow < until && !await condition()) await Task.Delay(150, Ct);
        (await condition()).ShouldBeTrue("Hết thời gian chờ");
    }

    // ---------- CV-01 → CV-05 ----------

    [Fact]
    public async Task Task_kanban_lap_lai_checklist_binh_luan()
    {
        var admin = await SignIn(Admin);
        var created = await Ok(await admin.PostAsJsonAsync("/api/v1/work/tasks", NewTask(ETung, "daily", Today), Ct));
        var id = created.GetProperty("id").GetGuid();
        created.GetProperty("code").GetString()!.ShouldStartWith("TK-");
        created.GetProperty("checklist").GetArrayLength().ShouldBe(2);

        // CV-01.E3 — lặp mà không có hạn
        await Fails(await admin.PostAsJsonAsync("/api/v1/work/tasks", NewTask(ETung, "weekly"), Ct), HttpStatusCode.BadRequest, "input.due");
        // CV-01.E4 — gắn lead không tồn tại
        await Fails(await admin.PostAsJsonAsync("/api/v1/work/tasks", new
        {
            title = "Gọi lead", assigneeId = ETung, priority = "normal", repeat = "none", link = new { type = "lead", id = Guid.NewGuid() },
        }, Ct), HttpStatusCode.BadRequest, "work.task.link_not_found");

        // Người làm: thấy ở Việc của tôi, tick checklist, bình luận, gửi duyệt
        var tung = await SignIn(Tung);
        var mine = await Ok(await tung.GetAsync("/api/v1/work/me/tasks", Ct));
        mine.GetProperty("items").EnumerateArray().ShouldContain(t => t.GetProperty("id").GetGuid() == id);
        var item = created.GetProperty("checklist")[0].GetProperty("id").GetGuid();
        var ticked = await Ok(await tung.PutAsJsonAsync($"/api/v1/work/tasks/{id}/checklist/{item}", new { done = true }, Ct));
        ticked.GetProperty("checklist")[0].GetProperty("done").GetBoolean().ShouldBeTrue();
        var commented = await Ok(await tung.PostAsJsonAsync($"/api/v1/work/tasks/{id}/comments", new { text = "Đang làm" }, Ct));
        commented.GetProperty("comments")[0].GetProperty("by").GetString().ShouldBe("Nguyễn Thanh Tùng");
        (await Ok(await tung.PostAsJsonAsync($"/api/v1/work/tasks/{id}/status", new { status = "review" }, Ct)))
            .GetProperty("task").GetProperty("status").GetString().ShouldBe("review");
        // Người làm không phải người giao, không có quyền xóa
        await Fails(await tung.DeleteAsync($"/api/v1/work/tasks/{id}", Ct), HttpStatusCode.Forbidden, "work.task.forbidden");

        // Người không dính tới task → không thấy (404)
        var hang = await SignIn(Hang);
        await Fails(await hang.PostAsJsonAsync($"/api/v1/work/tasks/{id}/status", new { status = "done" }, Ct), HttpStatusCode.NotFound,
            "work.task.not_found");

        // CV-02 — hoàn thành task lặp sinh lần kế tiếp đúng 1 lần (E2)
        var done = await Ok(await admin.PostAsJsonAsync($"/api/v1/work/tasks/{id}/status", new { status = "done" }, Ct));
        done.GetProperty("task").GetProperty("completedAt").ValueKind.ShouldNotBe(JsonValueKind.Null);
        var next = done.GetProperty("next");
        next.GetProperty("due").GetString().ShouldBe(D(Today.AddDays(1)));
        next.GetProperty("checklist").EnumerateArray().ShouldAllBe(c => !c.GetProperty("done").GetBoolean());
        await Ok(await admin.PostAsJsonAsync($"/api/v1/work/tasks/{id}/status", new { status = "doing", comment = "Làm lại phần 2" }, Ct));
        var again = await Ok(await admin.PostAsJsonAsync($"/api/v1/work/tasks/{id}/status", new { status = "done" }, Ct));
        (!again.TryGetProperty("next", out var none) || none.ValueKind == JsonValueKind.Null).ShouldBeTrue(); // null bị bỏ khỏi JSON

        // Xóa → Thùng rác → khôi phục: task phải lưu lại thật (handler lưu DbContext của Work)
        var nextId = next.GetProperty("id").GetGuid();
        (await admin.DeleteAsync($"/api/v1/work/tasks/{nextId}", Ct)).StatusCode.ShouldBe(HttpStatusCode.NoContent);
        Guid? trashId = null;
        for (var i = 0; i < 40 && trashId is null; i++)
        {
            await using (var scope = api.Scope())
                trashId = await scope.ServiceProvider.GetRequiredService<JPCenter.Modules.System.Infrastructure.Persistence.SystemDbContext>().TrashItems
                    .AsNoTracking().Where(t => t.Kind == "work.task" && t.EntityId == nextId).Select(t => (Guid?)t.Id.Value).FirstOrDefaultAsync(Ct);
            if (trashId is null) await Task.Delay(250, Ct);
        }
        trashId.ShouldNotBeNull();
        await Ok(await admin.PostAsync($"/api/v1/system/trash/{trashId}/restore", null, Ct));
        await using (var scope = api.Scope())
            (await scope.ServiceProvider.GetRequiredService<WorkDbContext>().Tasks.AsNoTracking().AnyAsync(t => t.Id == new JPCenter.Modules.Work.Domain.Tasks.WorkTaskId(nextId), Ct)).ShouldBeTrue();
    }

    [Fact]
    public async Task Giao_viec_cap_duoi_va_sai_quyen()
    {
        var manager = await SignIn(Manager);
        var reports = await Ok(await manager.GetAsync("/api/v1/work/team/reports", Ct));
        var list = reports.EnumerateArray().ToList();
        list.ShouldContain(r => r.GetProperty("id").GetGuid() == ETung && r.GetProperty("level").GetInt32() == 1);
        list.ShouldNotContain(r => r.GetProperty("id").GetGuid() == EKhoa);

        var body = new
        {
            assigneeIds = new[] { ETung, EBao }, title = "Gọi lại lead hẹn test", description = (string?)null, watcherIds = Array.Empty<Guid>(),
            priority = "normal", due = D(Today.AddDays(2)), repeat = "none", link = (object?)null, checklist = (string[]?)null,
        };
        var created = await Ok(await manager.PostAsJsonAsync("/api/v1/work/team/tasks", body, Ct));
        created.GetArrayLength().ShouldBe(2);
        created.EnumerateArray().Select(t => t.GetProperty("code").GetString()).Distinct().Count().ShouldBe(2);

        // Người làm không phải cấp dưới → 403
        await Fails(await manager.PostAsJsonAsync("/api/v1/work/team/tasks", body with { assigneeIds = new[] { EKhoa } }, Ct),
            HttpStatusCode.Forbidden, "work.task.not_your_report");
        var onlyMine = await Ok(await manager.GetAsync("/api/v1/work/team/tasks?onlyMine=true", Ct));
        onlyMine.GetProperty("items").EnumerateArray().ShouldAllBe(t => t.GetProperty("creatorId").GetGuid() == SeedData.IdOf("e-nam"));

        // Task toàn hệ thống: Sale không có work.tasks.create
        var tung = await SignIn(Tung);
        (await tung.PostAsJsonAsync("/api/v1/work/tasks", NewTask(EHang), Ct)).StatusCode.ShouldBe(HttpStatusCode.Forbidden);
        // Sale có work.tasks.view phạm vi own → "tất cả" chỉ là task mình dính tới
        var all = await Ok(await tung.GetAsync("/api/v1/work/tasks?scope=all", Ct));
        all.GetProperty("items").EnumerateArray().ShouldAllBe(t => t.GetProperty("assigneeId").GetGuid() == ETung
            || t.GetProperty("creatorId").GetGuid() == ETung || t.GetProperty("watcherIds").EnumerateArray().Any(w => w.GetGuid() == ETung));
    }

    [Fact]
    public async Task Nhac_han_task_gom_theo_nguoi()
    {
        var admin = await SignIn(Admin);
        await Ok(await admin.PostAsJsonAsync("/api/v1/work/tasks", NewTask(EHang, due: Today), Ct));
        await using var scope = api.Scope();
        var result = await scope.ServiceProvider.GetRequiredService<ISender>().Send(new RemindDueTasksCommand(), Ct);
        result.IsSuccess.ShouldBeTrue();
        result.Value.ShouldBeGreaterThan(0);
    }

    // ---------- XC-01 → XC-05 ----------

    [Fact]
    public async Task Xep_ca_dang_ky_ran_ban_chep_tuan()
    {
        var admin = await SignIn(Admin);
        var tung = await SignIn(Tung);
        var nextWeek = Today.AddDays(7 - ((int)Today.DayOfWeek + 6) % 7 + 7); // thứ 2 tuần sau nữa
        var day = nextWeek.AddDays(2);

        // XC-04 — báo bận / rảnh; ngày đã qua bị khóa
        await Ok(await tung.PutAsJsonAsync("/api/v1/work/me/shift-registrations", new { date = D(day), shift = "evening", status = "busy" }, Ct));
        await Ok(await tung.PutAsJsonAsync("/api/v1/work/me/shift-registrations", new { date = D(day), shift = "morning", status = "available" }, Ct));
        await Fails(await tung.PutAsJsonAsync("/api/v1/work/me/shift-registrations", new { date = D(Today.AddDays(-1)), shift = "morning", status = "busy" }, Ct),
            HttpStatusCode.BadRequest, "work.shift.past_date");
        var mine = await Ok(await tung.GetAsync($"/api/v1/work/me/shift-registrations?from={D(nextWeek)}", Ct));
        mine.GetProperty("registrations").GetArrayLength().ShouldBeGreaterThanOrEqualTo(2);

        // XC-01.E2 — NV báo bận
        await Fails(await admin.PostAsJsonAsync("/api/v1/work/shifts/toggle", new { employeeId = ETung, date = D(day), shift = "evening" }, Ct),
            HttpStatusCode.Conflict, "work.shift.busy");
        // XC-01.E1 — ngày đã qua
        await Fails(await admin.PostAsJsonAsync("/api/v1/work/shifts/toggle", new { employeeId = ETung, date = D(Today.AddDays(-1)), shift = "morning" }, Ct),
            HttpStatusCode.BadRequest, "work.shift.past_date");
        // XC-01.E3 — NV không làm theo ca (Giáo vụ)
        await Fails(await admin.PostAsJsonAsync("/api/v1/work/shifts/toggle", new { employeeId = SeedData.IdOf("e-mai"), date = D(day), shift = "morning" }, Ct),
            HttpStatusCode.BadRequest, "work.shift.employee_invalid");

        // XC-02 — xếp theo đăng ký (1 request) → ca rảnh được xếp; chạy lại không thêm
        var applied = await Ok(await admin.PostAsJsonAsync("/api/v1/work/shifts/apply-registrations", new { branchId = Cg, week = D(nextWeek) }, Ct));
        applied.GetInt32().ShouldBeGreaterThanOrEqualTo(1);
        (await Ok(await admin.PostAsJsonAsync("/api/v1/work/shifts/apply-registrations", new { branchId = Cg, week = D(nextWeek) }, Ct))).GetInt32().ShouldBe(0);

        var week = await Ok(await admin.GetAsync($"/api/v1/work/shifts?branchId={Cg}&week={D(nextWeek)}", Ct));
        week.GetProperty("weekStart").GetString().ShouldBe(D(nextWeek));
        week.GetProperty("assignments").EnumerateArray().ShouldContain(a => a.GetProperty("employeeId").GetGuid() == ETung
            && a.GetProperty("date").GetString() == D(day) && a.GetProperty("shift").GetString() == "morning");
        week.GetProperty("coverage").GetArrayLength().ShouldBe(21);
        week.GetProperty("employees").EnumerateArray().ShouldContain(e => e.GetProperty("id").GetGuid() == EHang);

        // XC-02 — chép sang tuần sau đó: ô trống được chép
        var copied = await Ok(await admin.PostAsJsonAsync("/api/v1/work/shifts/copy-week", new { branchId = Cg, week = D(nextWeek.AddDays(7)) }, Ct));
        copied.GetInt32().ShouldBeGreaterThanOrEqualTo(1);

        // Sale chỉ xem (work.shifts.view own), không xếp được
        (await tung.GetAsync($"/api/v1/work/shifts?branchId={Cg}", Ct)).StatusCode.ShouldBe(HttpStatusCode.OK);
        (await tung.PostAsJsonAsync("/api/v1/work/shifts/toggle", new { employeeId = EHang, date = D(day), shift = "morning" }, Ct))
            .StatusCode.ShouldBe(HttpStatusCode.Forbidden);
    }

    [Fact]
    public async Task Nghi_viec_go_ca_tu_hom_nay()
    {
        var admin = await SignIn(Admin);
        var employee = await Ok(await admin.PostAsJsonAsync("/api/v1/hr/employees", new
        {
            fullName = "Ca Thử Nghỉ", gender = "male", dob = (string?)null, phone = "0912000999", email = $"ca{Guid.NewGuid():N}"[..18] + "@test.vn",
            branchId = Cg, department = "reception", position = "Lễ tân", managerId = SeedData.IdOf("e-nam"), shiftBased = true, status = "active",
            contract = new { type = "fixed", start = D(Today.AddDays(-30)), end = D(Today.AddYears(1)), baseSalary = 7_000_000, allowance = (decimal?)null },
            teacher = (object?)null, note = (string?)null,
        }, Ct));
        var id = employee.GetProperty("id").GetGuid();
        await Ok(await admin.PostAsJsonAsync("/api/v1/work/shifts/toggle", new { employeeId = id, date = D(Today.AddDays(3)), shift = "afternoon" }, Ct));
        await Ok(await admin.PostAsJsonAsync($"/api/v1/hr/employees/{id}/quit", new { reason = "Chuyển công tác", quitAt = (string?)null }, Ct));

        await WaitUntil(async () =>
        {
            await using var scope = api.Scope();
            return !await scope.ServiceProvider.GetRequiredService<WorkDbContext>().ShiftAssignments.AnyAsync(a => a.EmployeeId == id, Ct);
        });
    }

    // ---------- BG-01 → BG-03 ----------

    [Fact]
    public async Task Ban_giao_ca_va_nhan_ca()
    {
        var tung = await SignIn(Tung);
        var draft = await Ok(await tung.GetAsync("/api/v1/work/handovers/draft", Ct));
        draft.GetProperty("openingFloat").GetDecimal().ShouldBe(1_000_000);
        var handed = draft.GetProperty("handedShifts").EnumerateArray().Select(s => s.GetString()).ToHashSet();
        var shift = new[] { "morning", "afternoon", "evening" }.First(s => !handed.Contains(s));
        draft = await Ok(await tung.GetAsync($"/api/v1/work/handovers/draft?shift={shift}", Ct));
        var expected = draft.GetProperty("cashExpected").GetDecimal();
        draft.GetProperty("receivers").EnumerateArray().ShouldContain(r => r.GetProperty("id").GetGuid() == EHang);

        // BG-01.E1 — người nhận là chính mình; E3 — lệch tiền không ghi chú
        await Fails(await tung.PostAsJsonAsync("/api/v1/work/handovers", new { shift, toId = ETung, cashCounted = expected }, Ct),
            HttpStatusCode.BadRequest, "work.handover.invalid_receiver");
        await Fails(await tung.PostAsJsonAsync("/api/v1/work/handovers", new { shift, toId = EHang, cashCounted = expected + 50_000 }, Ct),
            HttpStatusCode.BadRequest, "work.handover.note_required");
        // Tiền hệ thống do server tính — trường lạ "cashExpected" bị từ chối
        (await tung.PostAsJsonAsync("/api/v1/work/handovers", new { shift, toId = EHang, cashCounted = expected, cashExpected = 0 }, Ct))
            .StatusCode.ShouldBe(HttpStatusCode.BadRequest);

        var created = await Ok(await tung.PostAsJsonAsync("/api/v1/work/handovers",
            new { shift, toId = EHang, cashCounted = expected, issues = "Máy in quầy kẹt giấy", pending = new[] { "Gọi lại PH bé An" } }, Ct));
        var id = created.GetProperty("id").GetGuid();
        created.GetProperty("cash").GetProperty("expected").GetDecimal().ShouldBe(expected);
        created.GetProperty("status").GetString().ShouldBe("pending");
        await Fails(await tung.PostAsJsonAsync("/api/v1/work/handovers", new { shift, toId = EHang, cashCounted = expected }, Ct),
            HttpStatusCode.Conflict, "work.handover.duplicate");

        // BG-02 — chỉ người nhận xác nhận
        await Fails(await tung.PostAsJsonAsync($"/api/v1/work/handovers/{id}/receive", new { counted = expected }, Ct), HttpStatusCode.Forbidden,
            "work.handover.not_receiver");
        var hang = await SignIn(Hang);
        var list = await Ok(await hang.GetAsync("/api/v1/work/handovers", Ct));
        list.GetProperty("pendingForMe").EnumerateArray().ShouldContain(h => h.GetProperty("id").GetGuid() == id);
        var itemId = created.GetProperty("pending")[0].GetProperty("id").GetGuid();
        await Fails(await hang.PutAsJsonAsync($"/api/v1/work/handovers/{id}/items/{itemId}", new { done = true }, Ct), HttpStatusCode.Conflict,
            "work.handover.not_received");
        var received = await Ok(await hang.PostAsJsonAsync($"/api/v1/work/handovers/{id}/receive", new { counted = expected, note = "Đủ" }, Ct));
        received.GetProperty("status").GetString().ShouldBe("received");
        await Fails(await hang.PostAsJsonAsync($"/api/v1/work/handovers/{id}/receive", new { counted = expected }, Ct), HttpStatusCode.Conflict,
            "work.handover.closed");
        var ticked = await Ok(await hang.PutAsJsonAsync($"/api/v1/work/handovers/{id}/items/{itemId}", new { done = true }, Ct));
        ticked.GetProperty("pending")[0].GetProperty("done").GetBoolean().ShouldBeTrue();

        // BG-03 — quản lý chi nhánh thấy; Sale chi nhánh khác (own) không thấy bàn giao của người khác
        var manager = await SignIn(Manager);
        (await Ok(await manager.GetAsync($"/api/v1/work/handovers?branchId={Cg}", Ct))).GetProperty("items").EnumerateArray()
            .ShouldContain(h => h.GetProperty("id").GetGuid() == id);
        var sale = await SignIn(Sale);
        (await Ok(await sale.GetAsync("/api/v1/work/handovers", Ct))).GetProperty("items").EnumerateArray()
            .ShouldNotContain(h => h.GetProperty("id").GetGuid() == id);
        // Giáo vụ không có work.handover.create
        (await (await SignIn("giaovu@jpcenter.vn")).GetAsync("/api/v1/work/handovers/draft", Ct)).StatusCode.ShouldBe(HttpStatusCode.Forbidden);
    }
}
