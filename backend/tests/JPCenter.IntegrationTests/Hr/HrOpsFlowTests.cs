using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text.Json;
using JPCenter.Infrastructure.Common.Persistence;
using JPCenter.IntegrationTests.Infrastructure;
using JPCenter.Modules.Hr.Domain.Employees;
using JPCenter.Modules.Hr.Infrastructure.Persistence;
using JPCenter.Modules.Work.Domain.Shifts;
using JPCenter.Modules.Work.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;

namespace JPCenter.IntegrationTests.Hr;

/// <summary>User story 13 — chấm công, đơn từ, tuyển dụng, onboarding trên API + PostgreSQL thật với dữ liệu mẫu.</summary>
[Collection(SeededCollection.Name)]
public sealed class HrOpsFlowTests(SeededApiFactory api)
{
    private const string Admin = "admin@jpcenter.vn", Manager = "quanly.caugiay@jpcenter.vn", Tung = "tung.tung@jpcenter.vn",
        Hang = "hang.hang@jpcenter.vn", Teacher = "giaovien@jpcenter.vn";
    private static CancellationToken Ct => TestContext.Current.CancellationToken;
    private static readonly Guid Cg = SeedData.IdOf("cg");
    private static readonly Guid ETung = SeedData.IdOf("e-tung");
    private static readonly Guid EHang = SeedData.IdOf("e-hang");
    private static readonly Guid EBao = SeedData.IdOf("e-bao");
    private static DateOnly Today => DateOnly.FromDateTime(DateTime.UtcNow.AddHours(7));

    private async Task<HttpClient> SignIn(string email)
    {
        var client = api.CreateClient();
        var r = await client.PostAsJsonAsync("/api/v1/system/auth/login", new { identifier = email, password = SeededApiFactory.Password, remember = false }, Ct);
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

    private static async Task Fails(HttpResponseMessage r, HttpStatusCode status, string code)
    {
        var body = await r.Content.ReadAsStringAsync(Ct);
        r.StatusCode.ShouldBe(status, body);
        body.ShouldContain($"\"{code}\"", Case.Sensitive, body);
    }

    private static string D(DateOnly d) => d.ToString("yyyy-MM-dd");

    /// <summary>Ngày làm việc (bỏ CN) cách hôm nay ít nhất <paramref name="days"/> ngày</summary>
    private static DateOnly Workday(int days)
    {
        var d = Today.AddDays(days);
        return d.DayOfWeek == DayOfWeek.Sunday ? d.AddDays(1) : d;
    }

    private static async Task WaitUntil(Func<Task<bool>> condition, int seconds = 15)
    {
        var until = DateTime.UtcNow.AddSeconds(seconds);
        while (DateTime.UtcNow < until && !await condition()) await Task.Delay(150, Ct);
        (await condition()).ShouldBeTrue("Hết thời gian chờ");
    }

    // ---------- CC-01 → CC-04 ----------

    [Fact]
    public async Task Check_in_check_out_va_bang_cong()
    {
        var tung = await SignIn(Tung);
        var first = await Ok(await tung.PostAsync("/api/v1/hr/me/check-in", null, Ct));
        var checkIn = first.GetProperty("record").GetProperty("checkIn").GetString();
        checkIn.ShouldNotBeNull();
        // CC-01.E4 — bấm lần 2 trả bản ghi cũ
        var again = await Ok(await tung.PostAsync("/api/v1/hr/me/check-in", null, Ct));
        again.GetProperty("record").GetProperty("id").GetGuid().ShouldBe(first.GetProperty("record").GetProperty("id").GetGuid());
        var outR = await Ok(await tung.PostAsync("/api/v1/hr/me/check-out", null, Ct));
        outR.GetProperty("record").GetProperty("checkOut").GetString().ShouldNotBeNull();

        var mine = await Ok(await tung.GetAsync("/api/v1/hr/me/timesheet", Ct));
        mine.GetProperty("days").GetArrayLength().ShouldBeGreaterThanOrEqualTo(28);
        mine.GetProperty("employee").GetProperty("id").GetGuid().ShouldBe(ETung);
        mine.GetProperty("today").GetProperty("checkIn").GetString().ShouldBe(checkIn);
        await Fails(await tung.GetAsync($"/api/v1/hr/me/timesheet?month={Today.AddMonths(1):yyyy-MM}", Ct), HttpStatusCode.BadRequest,
            "hr.timesheet.invalid_month");

        // CC-01.E1 — giáo viên không chấm công văn phòng (cổng GV không vào được trang cá nhân NV)
        var teacher = await SignIn(Teacher);
        (await teacher.PostAsync("/api/v1/hr/me/check-in", null, Ct)).StatusCode.ShouldBe(HttpStatusCode.Forbidden);

        // CC-03 — quản lý xem lưới; Sale không có hr.timesheets
        var admin = await SignIn(Admin);
        var board = await Ok(await admin.GetAsync($"/api/v1/hr/timesheets?branchId={Cg}&department=sales", Ct));
        var rows = board.GetProperty("rows").EnumerateArray().ToList();
        rows.ShouldContain(r => r.GetProperty("employee").GetProperty("id").GetGuid() == ETung);
        rows.ShouldAllBe(r => r.GetProperty("employee").GetProperty("department").GetString() == "sales");
        (await tung.GetAsync("/api/v1/hr/timesheets", Ct)).StatusCode.ShouldBe(HttpStatusCode.Forbidden);
    }

    [Fact]
    public async Task Sua_cong_bat_buoc_ly_do_khong_tu_sua()
    {
        var manager = await SignIn(Manager);
        var day = Today.AddDays(-2);
        var url = $"/api/v1/hr/timesheets/{EBao}/{D(day)}";
        var edited = await Ok(await manager.PutAsJsonAsync(url, new { checkIn = "08:05", checkOut = "17:40", reason = "Quên chấm — máy lỗi" }, Ct));
        edited.GetProperty("checkIn").GetString().ShouldBe("08:05");
        edited.GetProperty("edited").GetProperty("reason").GetString().ShouldBe("Quên chấm — máy lỗi");

        await Fails(await manager.PutAsJsonAsync(url, new { checkIn = "08:05", checkOut = "17:40", reason = "" }, Ct), HttpStatusCode.BadRequest,
            "hr.time_record.reason_required");
        await Fails(await manager.PutAsJsonAsync(url, new { checkIn = "17:00", checkOut = "08:00", reason = "x" }, Ct), HttpStatusCode.BadRequest,
            "hr.time_record.invalid_checkout");
        await Fails(await manager.PutAsJsonAsync($"/api/v1/hr/timesheets/{EBao}/{D(Today.AddDays(2))}", new { checkIn = "08:00", reason = "x" }, Ct),
            HttpStatusCode.BadRequest, "hr.time_record.future_date");

        var admin = await SignIn(Admin);
        var self = SeedData.IdOf("e-trang");
        await Fails(await admin.PutAsJsonAsync($"/api/v1/hr/timesheets/{self}/{D(day)}", new { checkIn = "08:00", reason = "x" }, Ct),
            HttpStatusCode.Forbidden, "hr.time_record.self_edit");
    }

    // ---------- DN-01 → DN-04 ----------

    [Fact]
    public async Task Don_nghi_phep_hai_cap_duyet_va_tru_phep()
    {
        var tung = await SignIn(Tung);
        var from = Workday(80);
        var to = from;
        while (HrDays(from, to) < 4) to = to.AddDays(1);
        decimal Balance() => api.Services.CreateScope().ServiceProvider.GetRequiredService<HrDbContext>().Employees.AsNoTracking()
            .Single(e => e.Id == new EmployeeId(ETung)).LeaveBalance;
        var before = Balance();

        // Nghỉ > 3 ngày → quản lý trực tiếp (e-nam) + Phó giám đốc (e-trang)
        var created = await Ok(await tung.PostAsJsonAsync("/api/v1/hr/me/requests",
            new { type = "leave", from = D(from), to = D(to), paidLeave = true, reason = "Việc gia đình" }, Ct));
        var id = created.GetProperty("id").GetGuid();
        created.GetProperty("code").GetString()!.ShouldStartWith($"DT-{Today.Year}");
        var steps = created.GetProperty("steps").EnumerateArray().ToList();
        steps.Count.ShouldBe(2);
        steps[0].GetProperty("approverId").GetGuid().ShouldBe(SeedData.IdOf("e-nam"));
        steps[1].GetProperty("approverId").GetGuid().ShouldBe(SeedData.IdOf("e-trang"));
        created.GetProperty("canCancel").GetBoolean().ShouldBeTrue();

        // DN-01.E4 — trùng khoảng
        await Fails(await tung.PostAsJsonAsync("/api/v1/hr/me/requests", new { type = "business_trip", from = D(to), to = D(to), reason = "x" }, Ct),
            HttpStatusCode.Conflict, "hr.request.overlap");

        // DN-02.E1 — Phó GĐ chưa tới lượt nhưng có hr.requests.approve → duyệt thay được; người không có quyền → not_your_turn
        var hang = await SignIn(Hang);
        await Fails(await hang.PostAsJsonAsync($"/api/v1/hr/requests/{id}/approve", new { }, Ct), HttpStatusCode.Forbidden, "hr.request.not_your_turn");

        var manager = await SignIn(Manager);
        var todo = await Ok(await manager.GetAsync("/api/v1/hr/requests?tab=todo", Ct));
        todo.GetProperty("items").EnumerateArray().ShouldContain(r => r.GetProperty("id").GetGuid() == id);
        var step1 = await Ok(await manager.PostAsJsonAsync($"/api/v1/hr/requests/{id}/approve", new { comment = "Ok" }, Ct));
        step1.GetProperty("status").GetString().ShouldBe("pending");
        step1.GetProperty("currentStep").GetInt32().ShouldBe(2);
        Balance().ShouldBe(before);

        var admin = await SignIn(Admin);
        var done = await Ok(await admin.PostAsJsonAsync($"/api/v1/hr/requests/{id}/approve", new { }, Ct));
        done.GetProperty("status").GetString().ShouldBe("approved");
        Balance().ShouldBe(before - HrDays(from, to));
        await Fails(await admin.PostAsJsonAsync($"/api/v1/hr/requests/{id}/approve", new { }, Ct), HttpStatusCode.Conflict, "hr.request.not_pending");

        // Bảng công tính "leave" cho các ngày đó (nếu thuộc tháng hiện tại thì kiểm được, không thì bỏ qua)
        var mine = await Ok(await tung.GetAsync("/api/v1/hr/me/requests", Ct));
        mine.GetProperty("items").EnumerateArray().ShouldContain(r => r.GetProperty("id").GetGuid() == id
            && r.GetProperty("steps")[1].GetProperty("approver").GetString()!.Contains("Phạm Thu Trang"));
    }

    private static int HrDays(DateOnly from, DateOnly to)
    {
        var n = 0;
        for (var d = from; d <= to; d = d.AddDays(1)) if (d.DayOfWeek != DayOfWeek.Sunday) n++;
        return n;
    }

    [Fact]
    public async Task Tu_choi_huy_don_va_khong_tu_duyet()
    {
        var tung = await SignIn(Tung);
        var manager = await SignIn(Manager);
        var admin = await SignIn(Admin);

        // DN-01.E5 — tạm ứng thiếu số tiền
        await Fails(await tung.PostAsJsonAsync("/api/v1/hr/me/requests", new { type = "advance", reason = "Ứng lương" }, Ct), HttpStatusCode.BadRequest,
            "hr.request.amount_required");
        // Tạm ứng → quản lý + Kế toán trưởng
        var adv = await Ok(await tung.PostAsJsonAsync("/api/v1/hr/me/requests", new { type = "advance", amount = 2_000_000, reason = "Ứng lương" }, Ct));
        adv.GetProperty("steps").GetArrayLength().ShouldBe(2);
        var advId = adv.GetProperty("id").GetGuid();

        // DN-02.E4 — từ chối không lý do
        await Fails(await manager.PostAsJsonAsync($"/api/v1/hr/requests/{advId}/reject", new { reason = "" }, Ct), HttpStatusCode.BadRequest,
            "hr.request.reason_required");
        var rejected = await Ok(await manager.PostAsJsonAsync($"/api/v1/hr/requests/{advId}/reject", new { reason = "Đã ứng tháng này" }, Ct));
        rejected.GetProperty("status").GetString().ShouldBe("rejected");

        // DN-03 — chỉ người gửi hủy, đơn còn chờ
        var late = await Ok(await tung.PostAsJsonAsync("/api/v1/hr/me/requests",
            new { type = "late_early", from = D(Today.AddDays(-1)), reason = "Kẹt xe" }, Ct));
        var lateId = late.GetProperty("id").GetGuid();
        await Fails(await admin.PostAsync($"/api/v1/hr/me/requests/{lateId}/cancel", null, Ct), HttpStatusCode.Forbidden, "hr.request.only_owner");
        (await Ok(await tung.PostAsync($"/api/v1/hr/me/requests/{lateId}/cancel", null, Ct))).GetProperty("status").GetString().ShouldBe("cancelled");
        await Fails(await tung.PostAsync($"/api/v1/hr/me/requests/{lateId}/cancel", null, Ct), HttpStatusCode.Conflict, "hr.request.not_pending");

        // DN-02.E3 — không tự duyệt đơn của mình (kể cả có quyền duyệt thay)
        var own = await Ok(await admin.PostAsJsonAsync("/api/v1/hr/me/requests",
            new { type = "proposal", title = "Mua máy chiếu", amount = 8_000_000, reason = "Phòng 203 hỏng máy" }, Ct));
        // Đề xuất > 5 triệu: quản lý trực tiếp (GĐ) → Kế toán trưởng → Giám đốc (không liền nhau nên không gộp)
        own.GetProperty("steps").GetArrayLength().ShouldBe(3);
        await Fails(await admin.PostAsJsonAsync($"/api/v1/hr/requests/{own.GetProperty("id").GetGuid()}/approve", new { }, Ct),
            HttpStatusCode.Forbidden, "hr.request.self_approve");

        // DN-04 — tab "all" cần hr.requests.view: Sale chỉ own (thấy đơn của mình), Lễ tân cũng vậy
        var all = await Ok(await tung.GetAsync("/api/v1/hr/requests?tab=all", Ct));
        all.GetProperty("items").EnumerateArray().ShouldAllBe(r => r.GetProperty("employeeId").GetGuid() == ETung);
        var adminAll = await Ok(await admin.GetAsync("/api/v1/hr/requests?tab=all&type=advance", Ct));
        adminAll.GetProperty("items").EnumerateArray().ShouldAllBe(r => r.GetProperty("type").GetString() == "advance");
    }

    [Fact]
    public async Task Doi_ca_duyet_xong_work_doi_nguoi()
    {
        var admin = await SignIn(Admin);
        var day = Workday(20);
        async Task<Guid> Assign(Guid employee, string shift)
        {
            var r = await Ok(await admin.PostAsJsonAsync("/api/v1/work/shifts/toggle", new { employeeId = employee, date = D(day), shift, assign = true }, Ct));
            return r.GetProperty("assignment").GetProperty("id").GetGuid();
        }
        var mine = await Assign(ETung, "morning");
        var theirs = await Assign(EHang, "evening");

        var tung = await SignIn(Tung);
        // DN-01.E6 — đổi với chính mình / ca không phải của mình
        await Fails(await tung.PostAsJsonAsync("/api/v1/hr/me/requests",
                new { type = "shift_swap", swap = new { myShiftId = theirs, withEmployeeId = EHang, theirShiftId = mine }, reason = "x" }, Ct),
            HttpStatusCode.BadRequest, "hr.request.invalid_swap");
        var req = await Ok(await tung.PostAsJsonAsync("/api/v1/hr/me/requests",
            new { type = "shift_swap", swap = new { myShiftId = mine, withEmployeeId = EHang, theirShiftId = theirs }, reason = "Có lịch khám" }, Ct));
        req.GetProperty("from").GetString().ShouldBe(D(day));

        // XC-01.E6 — bỏ ca đang có đơn đổi chờ duyệt
        await Fails(await admin.PostAsJsonAsync("/api/v1/work/shifts/toggle", new { employeeId = ETung, date = D(day), shift = "morning", assign = false }, Ct),
            HttpStatusCode.Conflict, "work.shift.swap_pending");

        var manager = await SignIn(Manager);
        (await Ok(await manager.PostAsJsonAsync($"/api/v1/hr/requests/{req.GetProperty("id").GetGuid()}/approve", new { }, Ct)))
            .GetProperty("status").GetString().ShouldBe("approved");

        await WaitUntil(async () =>
        {
            await using var scope = api.Scope();
            var db = scope.ServiceProvider.GetRequiredService<WorkDbContext>();
            var a = await db.ShiftAssignments.AsNoTracking().SingleAsync(x => x.Id == new ShiftAssignmentId(mine), Ct);
            var b = await db.ShiftAssignments.AsNoTracking().SingleAsync(x => x.Id == new ShiftAssignmentId(theirs), Ct);
            return a.EmployeeId == EHang && b.EmployeeId == ETung;
        });
    }

    // ---------- TD-01 → TD-04, OB-01, OB-02 ----------

    [Fact]
    public async Task Tuyen_dung_den_onboarding_va_tao_ho_so()
    {
        var admin = await SignIn(Admin);
        var job = await Ok(await admin.PostAsJsonAsync("/api/v1/hr/jobs", new
        {
            title = "Lễ tân ca tối", branchId = Cg, department = "reception", kind = "staff", quantity = 1, salaryRange = "7–8 triệu",
            deadline = D(Today.AddDays(20)), channels = new[] { "TopCV", "Facebook" }, description = (string?)null,
        }, Ct));
        var jobId = job.GetProperty("id").GetGuid();
        job.GetProperty("status").GetString().ShouldBe("open");

        var email = $"ung.vien.{Guid.NewGuid():N}"[..24] + "@test.vn";
        var body = new { jobId, fullName = "Ứng Viên Thử", phone = "0987 000 111", email, source = "TopCV", japanese = (string?)null, expectedSalary = 7_500_000 };
        var cand = await Ok(await admin.PostAsJsonAsync("/api/v1/hr/candidates", body, Ct));
        var id = cand.GetProperty("candidate").GetProperty("id").GetGuid();
        cand.GetProperty("candidate").GetProperty("stage").GetString().ShouldBe("applied");
        // TD-02.E2 — trùng SĐT / email trong cùng tin
        await Fails(await admin.PostAsJsonAsync("/api/v1/hr/candidates", body with { email = "khac@test.vn" }, Ct), HttpStatusCode.Conflict,
            "hr.candidate.duplicate");
        // Kéo thẳng vào Offer phải qua thao tác riêng
        await Fails(await admin.PostAsJsonAsync($"/api/v1/hr/candidates/{id}/stage", new { stage = "offer" }, Ct), HttpStatusCode.BadRequest,
            "hr.candidate.stage_action");

        var interview = await Ok(await admin.PostAsJsonAsync($"/api/v1/hr/candidates/{id}/interview",
            new { at = DateTimeOffset.UtcNow.AddDays(1), interviewerId = SeedData.IdOf("e-nam"), mode = "offline", score = 8, note = "Giao tiếp tốt" }, Ct));
        interview.GetProperty("candidate").GetProperty("stage").GetString().ShouldBe("interview");
        interview.GetProperty("candidate").GetProperty("interview").GetProperty("interviewer").GetString().ShouldBe("Lê Hoàng Nam");

        await Fails(await admin.PostAsJsonAsync($"/api/v1/hr/candidates/{id}/offer", new { salary = 7_800_000, startDate = D(Today.AddDays(-1)) }, Ct),
            HttpStatusCode.BadRequest, "hr.candidate.offer_invalid");
        var start = Today.AddDays(7);
        await Ok(await admin.PostAsJsonAsync($"/api/v1/hr/candidates/{id}/offer", new { salary = 7_800_000, startDate = D(start) }, Ct));
        await Ok(await admin.PostAsJsonAsync($"/api/v1/hr/candidates/{id}/notes", new { text = "Đã gọi xác nhận offer" }, Ct));

        // TD-04 — nhận việc → phiếu onboarding, đủ số lượng → tự đóng tin
        var hired = await Ok(await admin.PostAsync($"/api/v1/hr/candidates/{id}/hire", null, Ct));
        hired.GetProperty("candidate").GetProperty("stage").GetString().ShouldBe("hired");
        hired.GetProperty("job").GetProperty("status").GetString().ShouldBe("closed");
        var onboardingId = hired.GetProperty("onboardingId").GetGuid();
        await Fails(await admin.PostAsJsonAsync($"/api/v1/hr/candidates/{id}/reject", new { reason = "x" }, Ct), HttpStatusCode.Conflict,
            "hr.candidate.hired");
        // TD-02.E1 — tin đã đóng
        await Fails(await admin.PostAsJsonAsync("/api/v1/hr/candidates", body with { phone = "0911222333", email = "moi@test.vn" }, Ct),
            HttpStatusCode.Conflict, "hr.job.closed");
        // Xóa tin đã có ứng viên
        await Fails(await admin.DeleteAsync($"/api/v1/hr/jobs/{jobId}", Ct), HttpStatusCode.Conflict, "hr.job.has_candidates");

        var list = await Ok(await admin.GetAsync("/api/v1/hr/onboardings?filter=active", Ct));
        var ob = list.EnumerateArray().Single(o => o.GetProperty("id").GetGuid() == onboardingId);
        ob.GetProperty("startDate").GetString().ShouldBe(D(start));
        ob.GetProperty("items").EnumerateArray().ShouldNotContain(i => i.GetProperty("key").GetString() == "demo");

        var ticked = await Ok(await admin.PutAsJsonAsync($"/api/v1/hr/onboardings/{onboardingId}/items/contract", new { done = true }, Ct));
        var item = ticked.GetProperty("items").EnumerateArray().Single(i => i.GetProperty("key").GetString() == "contract");
        item.GetProperty("done").GetBoolean().ShouldBeTrue();
        item.GetProperty("doneBy").GetString().ShouldBe("Phạm Thu Trang");

        // OB-02 — tạo hồ sơ, gắn vào phiếu; tạo lần 2 → already_created
        var withEmployee = await Ok(await admin.PostAsJsonAsync($"/api/v1/hr/onboardings/{onboardingId}/employee", new { gender = "female" }, Ct));
        var employeeId = withEmployee.GetProperty("employeeId").GetGuid();
        var profile = await Ok(await admin.GetAsync($"/api/v1/hr/employees/{employeeId}", Ct));
        profile.GetProperty("status").GetString().ShouldBe("probation");
        profile.GetProperty("contract").GetProperty("baseSalary").GetDecimal().ShouldBe(7_800_000);
        await Fails(await admin.PostAsJsonAsync($"/api/v1/hr/onboardings/{onboardingId}/employee", new { gender = "female" }, Ct),
            HttpStatusCode.Conflict, "hr.onboarding.already_created");
    }

    [Fact]
    public async Task Tuyen_dung_sai_quyen()
    {
        var tung = await SignIn(Tung);
        (await tung.GetAsync("/api/v1/hr/jobs", Ct)).StatusCode.ShouldBe(HttpStatusCode.Forbidden);
        (await tung.GetAsync("/api/v1/hr/onboardings", Ct)).StatusCode.ShouldBe(HttpStatusCode.Forbidden);
        // Sale có hr.requests.create phạm vi own → chỉ tạo hộ cấp dưới; người ngoài phạm vi coi như không có
        await Fails(await tung.PostAsJsonAsync("/api/v1/hr/requests", new { type = "advance", amount = 1, reason = "x", employeeId = EHang }, Ct),
            HttpStatusCode.NotFound, "hr.request.employee_not_found");
        // Giáo viên (cổng GV) không vào được đơn từ cổng nhân viên
        (await (await SignIn(Teacher)).GetAsync("/api/v1/hr/requests", Ct)).StatusCode.ShouldBe(HttpStatusCode.Forbidden);
    }
}
