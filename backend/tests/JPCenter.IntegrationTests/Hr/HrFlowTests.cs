using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text.Json;
using JPCenter.Application.Common.Messaging;
using JPCenter.Infrastructure.Common.Persistence;
using JPCenter.IntegrationTests.Infrastructure;
using JPCenter.Modules.Hr.Application.Employees.ApplyScheduledQuits;
using JPCenter.Modules.Hr.Application.Employees.RemindExpiringContracts;
using JPCenter.Modules.Hr.Domain.Employees;
using JPCenter.Modules.Hr.Infrastructure.Persistence;
using JPCenter.Modules.System.Domain.Users;
using JPCenter.Modules.System.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;

namespace JPCenter.IntegrationTests.Hr;

/// <summary>User story 04 — quản lý nhân sự, trên API + PostgreSQL thật với dữ liệu mẫu.</summary>
[Collection(SeededCollection.Name)]
public sealed class HrFlowTests(SeededApiFactory api)
{
    private const string Staff = "/api/v1/hr/employees";
    private const string Teachers = "/api/v1/hr/teachers";
    private static CancellationToken Ct => TestContext.Current.CancellationToken;
    private static int _seq;

    private static readonly Guid Cg = SeedData.IdOf("cg");
    private static readonly Guid Q1 = SeedData.IdOf("q1");
    private static DateOnly Today => DateOnly.FromDateTime(DateTime.UtcNow.AddHours(7));

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

    private static async Task<string> Code(HttpResponseMessage r)
    {
        var body = await Json(r);
        return body.TryGetProperty("code", out var c) ? c.GetString()! : body.GetProperty("errors").EnumerateObject().First().Value[0].GetString()!;
    }

    private static async Task<JsonElement> Ok(HttpResponseMessage r)
    {
        var body = await r.Content.ReadAsStringAsync(Ct);
        r.IsSuccessStatusCode.ShouldBeTrue(body);
        return JsonDocument.Parse(body).RootElement;
    }

    private static bool IsNull(JsonElement e, string name) => !e.TryGetProperty(name, out var v) || v.ValueKind == JsonValueKind.Null;

    private static string NewEmail(string prefix = "nv") => $"{prefix}{Interlocked.Increment(ref _seq)}.{Guid.NewGuid():N}"[..20] + "@test.vn";

    private static object NewStaff(Guid branchId, string? email = null, Guid? managerId = null, DateOnly? end = null, string type = "fixed",
        string? name = null) => new
    {
        fullName = name ?? "Nhân Viên Thử", gender = "female", dob = "1995-05-05", phone = "0912345678", email = email ?? NewEmail(), branchId,
        department = "sales", position = "Sale", managerId, shiftBased = true, status = type == "probation" ? "probation" : "active",
        contract = new { type, start = Today.AddDays(-30).ToString("yyyy-MM-dd"), end = (end ?? Today.AddYears(1)).ToString("yyyy-MM-dd"),
            baseSalary = 9_000_000, allowance = 500_000 },
        teacher = (object?)null, note = (string?)null,
    };

    private static object NewTeacher(Guid branchId, params Guid[] extra) => new
    {
        kind = "teacher", fullName = "Giáo Viên Thử", gender = "male", dob = (string?)null, phone = "0912345679", email = NewEmail("gv"), branchId,
        department = "teaching", position = "Giáo viên", managerId = (Guid?)null, shiftBased = false, status = "active",
        contract = new { type = "indefinite", start = "2026-01-01", end = (string?)null, baseSalary = (decimal?)null, allowance = (decimal?)null },
        teacher = new { type = "vn", levels = new[] { "N5", "N4" }, certificates = new[] { "JLPT N1" }, rate = new { offline = 250_000, online = 200_000 },
            extraBranchIds = extra, bio = (string?)null },
        note = (string?)null,
    };

    private static object UpdateBody(JsonElement e, Guid? managerId = null, Guid? branchId = null, string? email = null, string? status = null,
        string? phone = null) => new
    {
        fullName = e.GetProperty("fullName").GetString(), gender = e.GetProperty("gender").GetString(), dob = (string?)null,
        phone = phone ?? e.GetProperty("phone").GetString(), email = email ?? e.GetProperty("email").GetString(),
        branchId = branchId ?? e.GetProperty("branchId").GetGuid(), department = e.GetProperty("department").GetString(),
        position = e.GetProperty("position").GetString(), managerId, shiftBased = e.GetProperty("shiftBased").GetBoolean(),
        status = status ?? e.GetProperty("status").GetString(), teacher = (object?)null, note = "đã sửa", version = e.GetProperty("version").GetUInt32(),
    };

    private async Task<JsonElement> Create(HttpClient client, object body, string url = Staff)
    {
        var r = await client.PostAsJsonAsync(url, body, Ct);
        r.StatusCode.ShouldBe(HttpStatusCode.Created, await r.Content.ReadAsStringAsync(Ct));
        return await Json(r);
    }

    private static async Task WaitUntil(Func<Task<bool>> condition, int seconds = 10)
    {
        var until = DateTime.UtcNow.AddSeconds(seconds);
        while (DateTime.UtcNow < until && !await condition()) await Task.Delay(100, Ct);
        (await condition()).ShouldBeTrue("Hết thời gian chờ");
    }

    private async Task<T> Send<T>(ICommand<T> command)
    {
        await using var scope = api.Scope();
        var result = await scope.ServiceProvider.GetRequiredService<ISender>().Send(command, Ct);
        result.IsSuccess.ShouldBeTrue(result.IsFailure ? result.Error.Message : null);
        return result.Value;
    }

    // ---------- NS-01 / NS-02 / NS-05 ----------

    [Fact]
    public async Task Danh_sach_theo_trang_va_pham_vi_chi_nhanh()
    {
        var admin = await SignIn("admin@jpcenter.vn");
        var staff = await Ok(await admin.GetAsync(Staff, Ct));
        staff.GetProperty("items").EnumerateArray().ShouldAllBe(e => e.GetProperty("kind").GetString() == "staff");
        staff.GetProperty("items").EnumerateArray().ShouldNotContain(e => e.GetProperty("status").GetString() == "quit"); // mặc định ẩn đã nghỉ
        staff.GetProperty("stats").GetProperty("active").GetInt32().ShouldBeGreaterThan(0);
        (await Ok(await admin.GetAsync($"{Staff}?includeQuit=true&status=quit", Ct))).GetProperty("items").EnumerateArray()
            .ShouldContain(e => e.GetProperty("code").GetString() == "NV0122");

        var teachers = await Ok(await admin.GetAsync($"{Teachers}?level=N3", Ct));
        teachers.GetProperty("items").EnumerateArray().ShouldAllBe(e => e.GetProperty("kind").GetString() != "staff");
        teachers.GetProperty("items").EnumerateArray().First().GetProperty("teacher").GetProperty("rate").GetProperty("offline").GetDecimal()
            .ShouldBeGreaterThan(0);

        // Hồ sơ GV không mở được qua trang nhân viên
        (await admin.GetAsync($"{Staff}/{SeedData.IdOf("t-ha")}", Ct)).StatusCode.ShouldBe(HttpStatusCode.NotFound);

        // Quản lý Cầu Giấy: chỉ thấy Cầu Giấy; hồ sơ Q1 coi như không tồn tại
        var bm = await SignIn("quanly.caugiay@jpcenter.vn");
        (await Ok(await bm.GetAsync(Staff, Ct))).GetProperty("items").EnumerateArray().ShouldAllBe(e => e.GetProperty("branchId").GetGuid() == Cg);
        (await bm.GetAsync($"{Staff}/{SeedData.IdOf("e-q1m")}", Ct)).StatusCode.ShouldBe(HttpStatusCode.NotFound);

        // Sale không có quyền trang nhân sự
        var sale = await SignIn("tung.tung@jpcenter.vn");
        (await sale.GetAsync(Staff, Ct)).StatusCode.ShouldBe(HttpStatusCode.Forbidden);
        (await sale.PostAsJsonAsync(Teachers, NewTeacher(Cg), Ct)).StatusCode.ShouldBe(HttpStatusCode.Forbidden);
    }

    [Fact]
    public async Task Chi_quyen_xem_thi_an_luong_va_pham_vi_own_chi_thay_minh()
    {
        // Cấp lẻ "xem nhân viên" phạm vi own cho Marketing (không có cấp dưới, không có quyền lương)
        var sa = await SignIn("superadmin@jpcenter.vn");
        var target = SeedData.IdOf("e-baoanh");
        await using (var scope = api.Scope())
        {
            var account = await scope.ServiceProvider.GetRequiredService<SystemDbContext>().UserAccounts.AsNoTracking()
                .SingleAsync(a => a.Email == "marketing@jpcenter.vn", Ct);
            (await sa.PostAsJsonAsync($"/api/v1/system/users/{account.Id.Value}/overrides",
                new { perm = "hr.employees.view", effect = "grant", scope = "own", reason = "Xem hồ sơ của mình" }, Ct)).IsSuccessStatusCode.ShouldBeTrue();
        }

        var me = await SignIn("marketing@jpcenter.vn");
        var items = (await Ok(await me.GetAsync(Staff, Ct))).GetProperty("items").EnumerateArray().ToList();
        items.Count.ShouldBe(1);
        items[0].GetProperty("id").GetGuid().ShouldBe(target);
        items[0].GetProperty("salaryHidden").GetBoolean().ShouldBeTrue();
        IsNull(items[0].GetProperty("contract"), "baseSalary").ShouldBeTrue();
        (await me.PutAsJsonAsync($"{Staff}/{target}", UpdateBody(items[0]), Ct)).StatusCode.ShouldBe(HttpStatusCode.Forbidden);

        // Quản lý Cầu Giấy (có quyền sửa + bảng lương) thấy lương
        var bm = await SignIn("quanly.caugiay@jpcenter.vn");
        (await Ok(await bm.GetAsync($"{Staff}/{target}", Ct))).GetProperty("salaryHidden").GetBoolean().ShouldBeFalse();
    }

    // ---------- NS-03 ----------

    [Fact]
    public async Task Them_ho_so_sinh_ma_kiem_tra_trung_email_va_chi_nhanh_duoc_gan()
    {
        var bm = await SignIn("quanly.caugiay@jpcenter.vn");
        var e = await Create(bm, NewStaff(Cg));
        e.GetProperty("code").GetString()!.ShouldStartWith("NV");
        e.GetProperty("contract").GetProperty("no").GetString()!.ShouldContain(e.GetProperty("code").GetString()!);
        (e.GetProperty("leaveBalance").GetDecimal(), e.GetProperty("hasAccount").GetBoolean()).ShouldBe((12m, false));

        (await Code(await bm.PostAsJsonAsync(Staff, NewStaff(Cg, email: "hong.hong@jpcenter.vn"), Ct))).ShouldBe("hr.employee.duplicate_email");
        (await Code(await bm.PostAsJsonAsync(Staff, NewStaff(Q1), Ct))).ShouldBe("hr.employee.branch_not_allowed");
        var invalid = new
        {
            fullName = "X", gender = "male", dob = (string?)null, phone = "0912345678", email = NewEmail(), branchId = Cg, department = "sales",
            position = "Sale", managerId = (Guid?)null, shiftBased = false, status = "quit",
            contract = new { type = "fixed", start = "2026-01-01", end = (string?)null, baseSalary = (decimal?)null, allowance = (decimal?)null },
            teacher = (object?)null, note = (string?)null,
        };
        (await bm.PostAsJsonAsync(Staff, invalid, Ct)).StatusCode.ShouldBe(HttpStatusCode.BadRequest); // trạng thái nghỉ việc + thiếu ngày kết thúc

        // GV: mã GV, phòng Giảng dạy, chi nhánh dạy thêm trùng chi nhánh chính tự bỏ
        var admin = await SignIn("admin@jpcenter.vn");
        var t = await Create(admin, NewTeacher(Cg, Cg, Q1), Teachers);
        t.GetProperty("code").GetString()!.ShouldStartWith("GV");
        t.GetProperty("department").GetString().ShouldBe("teaching");
        t.GetProperty("teacher").GetProperty("extraBranchIds").EnumerateArray().Select(x => x.GetGuid()).ShouldBe([Q1]);
        t.GetProperty("teacher").GetProperty("levels").EnumerateArray().Select(x => x.GetString()).ShouldBe(["N5", "N4"]);
    }

    // ---------- NS-04 ----------

    [Fact]
    public async Task Sua_ho_so_chan_vong_quan_ly_canh_bao_doi_chi_nhanh_va_sua_de()
    {
        var admin = await SignIn("admin@jpcenter.vn");
        var a = await Create(admin, NewStaff(Cg));
        var b = await Create(admin, NewStaff(Cg, managerId: a.GetProperty("id").GetGuid()));
        var aId = a.GetProperty("id").GetGuid();
        b.GetProperty("managerName").GetString().ShouldBe("Nhân Viên Thử");

        (await Code(await admin.PutAsJsonAsync($"{Staff}/{aId}", UpdateBody(a, managerId: b.GetProperty("id").GetGuid()), Ct)))
            .ShouldBe("hr.employee.manager_cycle");
        (await Code(await admin.PutAsJsonAsync($"{Staff}/{aId}", UpdateBody(a, managerId: aId), Ct))).ShouldBe("hr.employee.self_manager");
        (await Code(await admin.PutAsJsonAsync($"{Staff}/{aId}", UpdateBody(a, status: "quit"), Ct))).ShouldBe("hr.employee.status_not_allowed");

        var moved = await Ok(await admin.PutAsJsonAsync($"{Staff}/{aId}", UpdateBody(a, branchId: Q1, status: "leave"), Ct));
        moved.GetProperty("warnings").GetArrayLength().ShouldBe(1);
        (moved.GetProperty("employee").GetProperty("branchId").GetGuid(), moved.GetProperty("employee").GetProperty("status").GetString())
            .ShouldBe((Q1, "leave"));

        // Sửa bằng version cũ
        (await Code(await admin.PutAsJsonAsync($"{Staff}/{aId}", UpdateBody(a), Ct))).ShouldBe("system.concurrency");
    }

    // ---------- NS-06 ----------

    [Fact]
    public async Task Gia_han_hop_dong_luu_lich_su_va_chinh_thuc_hoa()
    {
        var admin = await SignIn("admin@jpcenter.vn");
        var e = await Create(admin, NewStaff(Cg, type: "probation", end: Today.AddDays(20)));
        var id = e.GetProperty("id").GetGuid();
        e.GetProperty("status").GetString().ShouldBe("probation");
        e.GetProperty("contractEndsIn").GetInt32().ShouldBe(20);

        var renewed = await Ok(await admin.PostAsJsonAsync($"{Staff}/{id}/contracts",
            new { type = "fixed", end = Today.AddYears(2).ToString("yyyy-MM-dd"), baseSalary = 12_000_000 }, Ct));
        var r = renewed;
        (r.GetProperty("status").GetString(), r.GetProperty("contract").GetProperty("type").GetString()).ShouldBe(("active", "fixed"));
        r.GetProperty("contract").GetProperty("baseSalary").GetDecimal().ShouldBe(12_000_000m);
        r.GetProperty("contract").GetProperty("allowance").GetDecimal().ShouldBe(500_000m); // bỏ trống → giữ như cũ
        r.GetProperty("contractHistory").GetArrayLength().ShouldBe(1);
        r.GetProperty("contractHistory")[0].GetProperty("type").GetString().ShouldBe("probation");
        r.GetProperty("contract").GetProperty("no").GetString().ShouldNotBe(e.GetProperty("contract").GetProperty("no").GetString());

        (await Code(await admin.PostAsJsonAsync($"{Staff}/{id}/contracts", new { type = "fixed" }, Ct))).ShouldBe("hr.employee.contract_end_required");
    }

    // ---------- NS-09 ----------

    [Fact]
    public async Task Nhac_hop_dong_con_45_15_7_ngay_qua_email()
    {
        var admin = await SignIn("admin@jpcenter.vn");
        var bm = await Create(admin, NewStaff(Cg, name: "Quản Lý Nhắc"));
        var in15 = await Create(admin, NewStaff(Cg, managerId: bm.GetProperty("id").GetGuid(), end: Today.AddDays(15), name: "Hết Hạn Mười Lăm"));
        await Create(admin, NewStaff(Cg, end: Today.AddDays(16), name: "Hết Hạn Mười Sáu"));

        await Send(new RemindExpiringContractsCommand());

        // Người có quyền sửa hồ sơ ở Cầu Giấy (quản lý chi nhánh) + quản lý trực tiếp
        var mails = api.Emails.Sent.Where(m => m.Text.Contains("Hết Hạn Mười Lăm")).ToList();
        mails.ShouldContain(m => m.To == "quanly.caugiay@jpcenter.vn");
        mails.ShouldContain(m => m.To == bm.GetProperty("email").GetString());
        mails.ShouldAllBe(m => m.Text.Contains("còn 15 ngày"));
        api.Emails.Sent.ShouldNotContain(m => m.Text.Contains("Hết Hạn Mười Sáu"));

        var ending = await Ok(await admin.GetAsync($"{Staff}?special=ending&q=het han muoi", Ct));
        ending.GetProperty("items").EnumerateArray().Select(x => x.GetProperty("id").GetGuid()).ShouldContain(in15.GetProperty("id").GetGuid());
    }

    // ---------- NS-07 + PQ-08.E6 ----------

    [Fact]
    public async Task Cap_tai_khoan_roi_cho_nghi_viec_thi_tai_khoan_bi_khoa()
    {
        var admin = await SignIn("admin@jpcenter.vn");
        var sa = await SignIn("superadmin@jpcenter.vn");
        var e = await Create(admin, NewStaff(Cg));
        var id = e.GetProperty("id").GetGuid();

        // System cấp tài khoản → sự kiện StaffAccountCreated → Hr đánh dấu "đã có tài khoản"
        (await sa.PostAsJsonAsync("/api/v1/system/users",
            new { employeeId = id, roles = new[] { new { roleId = SeedData.IdOf("R-SALE"), branchIds = new[] { Cg } } } }, Ct)).IsSuccessStatusCode.ShouldBeTrue();
        await WaitUntil(async () => (await Ok(await admin.GetAsync($"{Staff}/{id}", Ct))).GetProperty("hasAccount").GetBoolean());

        (await admin.PostAsJsonAsync($"{Staff}/{id}/quit", new { reason = "" }, Ct)).StatusCode.ShouldBe(HttpStatusCode.BadRequest);
        var quit = await Ok(await admin.PostAsJsonAsync($"{Staff}/{id}/quit", new { reason = "Chuyển công tác" }, Ct));
        (quit.GetProperty("employee").GetProperty("status").GetString(), quit.GetProperty("employee").GetProperty("quitReason").GetString())
            .ShouldBe(("quit", "Chuyển công tác"));
        (await Code(await admin.PostAsJsonAsync($"{Staff}/{id}/quit", new { reason = "x" }, Ct))).ShouldBe("hr.employee.already_quit");

        await WaitUntil(async () =>
        {
            await using var scope = api.Scope();
            var account = await scope.ServiceProvider.GetRequiredService<SystemDbContext>().UserAccounts.AsNoTracking()
                .SingleAsync(a => a.EmployeeId == id, Ct);
            return account.Status == UserAccountStatus.Locked;
        });

        // Người đã nghỉ: chỉ sửa SĐT + ghi chú
        var quitEmp = quit.GetProperty("employee");
        var edited = await Ok(await admin.PutAsJsonAsync($"{Staff}/{id}", UpdateBody(quitEmp, branchId: Q1, phone: "0999999999"), Ct));
        (edited.GetProperty("employee").GetProperty("phone").GetString(), edited.GetProperty("employee").GetProperty("branchId").GetGuid())
            .ShouldBe(("0999999999", Cg));
        edited.GetProperty("warnings").GetArrayLength().ShouldBe(1);
    }

    [Fact]
    public async Task Hen_ngay_nghi_job_doi_trang_thai_khi_toi_ngay_va_canh_bao_cap_duoi()
    {
        var admin = await SignIn("admin@jpcenter.vn");
        var boss = await Create(admin, NewStaff(Cg));
        var bossId = boss.GetProperty("id").GetGuid();
        await Create(admin, NewStaff(Cg, managerId: bossId));

        var scheduled = await Ok(await admin.PostAsJsonAsync($"{Staff}/{bossId}/quit",
            new { reason = "Hết hợp đồng", quitAt = Today.AddDays(10).ToString("yyyy-MM-dd") }, Ct));
        var emp = scheduled.GetProperty("employee");
        (emp.GetProperty("status").GetString(), emp.GetProperty("quitScheduled").GetBoolean()).ShouldBe(("active", true));
        scheduled.GetProperty("warnings").GetArrayLength().ShouldBe(2); // có cấp dưới + đã hẹn ngày

        await Send(new ApplyScheduledQuitsCommand());
        (await Ok(await admin.GetAsync($"{Staff}/{bossId}", Ct))).GetProperty("status").GetString().ShouldBe("active");

        // Tua tới ngày hẹn
        await using (var scope = api.Scope())
            await scope.ServiceProvider.GetRequiredService<HrDbContext>().Employees.Where(x => x.Id == new EmployeeId(bossId))
                .ExecuteUpdateAsync(s => s.SetProperty(x => x.QuitAt, Today), Ct);
        await Send(new ApplyScheduledQuitsCommand());
        (await Ok(await admin.GetAsync($"{Staff}/{bossId}", Ct))).GetProperty("status").GetString().ShouldBe("quit");
    }

    [Fact]
    public async Task Khong_cho_nghi_Super_Admin_cuoi_cung()
    {
        var admin = await SignIn("admin@jpcenter.vn");
        (await Code(await admin.PostAsJsonAsync($"{Staff}/{SeedData.IdOf("e-huy")}/quit", new { reason = "Thử" }, Ct)))
            .ShouldBe("hr.employee.last_super_admin");
    }

    // ---------- NS-08 / NS-10 ----------

    [Fact]
    public async Task Lich_ranh_giao_vien_va_nghi_dai_han()
    {
        var admin = await SignIn("admin@jpcenter.vn");
        var t = await Create(admin, NewTeacher(Cg), Teachers);
        var id = t.GetProperty("id").GetGuid();

        var saved = await Ok(await admin.PutAsJsonAsync($"{Teachers}/{id}/availability",
            new { availability = new Dictionary<string, string[]> { ["1"] = ["morning", "evening"], ["6"] = ["afternoon"] } }, Ct));
        var avail = saved.GetProperty("teacher").GetProperty("availability");
        avail.GetProperty("1").EnumerateArray().Select(x => x.GetString()).ShouldBe(["morning", "evening"]);

        // Sửa hồ sơ GV không làm mất lịch rảnh
        var body = UpdateBody(saved);
        var updated = await Ok(await admin.PutAsJsonAsync($"{Teachers}/{id}", body, Ct));
        updated.GetProperty("employee").GetProperty("teacher").GetProperty("availability").GetProperty("6").GetArrayLength().ShouldBe(1);

        (await Ok(await admin.PostAsync($"{Teachers}/{id}/long-leave", null, Ct))).GetProperty("status").GetString()
            .ShouldBe("leave");
        (await Code(await admin.PostAsync($"{Teachers}/{id}/long-leave", null, Ct))).ShouldBe("hr.employee.invalid_status");
        (await Ok(await admin.PostAsync($"{Teachers}/{id}/return", null, Ct))).GetProperty("status").GetString()
            .ShouldBe("active");

        // Nhân viên văn phòng không có lịch rảnh
        var staffId = SeedData.IdOf("e-hong");
        (await admin.PutAsJsonAsync($"{Teachers}/{staffId}/availability", new { availability = new Dictionary<string, string[]>() }, Ct))
            .StatusCode.ShouldBe(HttpStatusCode.NotFound);
    }
}
