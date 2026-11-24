using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text.Json;
using JPCenter.Infrastructure.Common.Persistence;
using JPCenter.IntegrationTests.Infrastructure;
using JPCenter.Modules.Finance.Contracts;
using Microsoft.Extensions.DependencyInjection;

namespace JPCenter.IntegrationTests.Finance;

/// <summary>
/// Giai đoạn 22 phase 3 — lương GV (TC-13), lương NV (TC-14), hoa hồng & KPI Sale (TC-12), Sale ghi nhận khi lập hóa đơn (TC-04.E5),
/// phiếu lương ở cổng GV. Tháng tương lai (2027-xx) để không đụng dữ liệu mẫu của test khác.
/// </summary>
[Collection(SeededCollection.Name)]
public sealed class PayrollFlowTests(SeededApiFactory api)
{
    private const string Finance = "/api/v1/finance";
    private static CancellationToken Ct => TestContext.Current.CancellationToken;
    private static readonly Guid Cg = SeedData.IdOf("cg");
    private static readonly Guid ClassN5K21 = SeedData.IdOf("C-N5K21");
    private static int _seq;

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

    private static async Task Eventually(Func<Task<bool>> condition)
    {
        for (var i = 0; i < 40; i++)
        {
            if (await condition()) return;
            await Task.Delay(250, Ct);
        }
        (await condition()).ShouldBeTrue("Hết thời gian chờ việc chạy nền");
    }

    private static IEnumerable<JsonElement> Rows(JsonElement board) => board.GetProperty("rows").EnumerateArray();

    [Fact(DisplayName = "TC-13: bảng lương GV — tiền dạy = Σ buổi đã xác nhận, thực nhận đúng công thức; tháng sai → invalid_month")]
    public async Task Teacher_payroll_board()
    {
        var admin = await SignIn();
        (await Code(await admin.GetAsync($"{Finance}/payroll/teacher?month=2026-13", Ct))).ShouldBe("finance.payroll.invalid_month");

        var checkedRows = 0;
        foreach (var month in new[] { DateTime.Today.ToString("yyyy-MM"), DateTime.Today.AddMonths(-1).ToString("yyyy-MM") })
        {
            var board = await Ok(await admin.GetAsync($"{Finance}/payroll/teacher?month={month}", Ct));
            board.GetProperty("month").GetString().ShouldBe(month);
            foreach (var r in Rows(board))
            {
                var items = r.GetProperty("items").EnumerateArray().ToList();
                var confirmed = items.Where(i => i.GetProperty("confirmed").GetBoolean()).ToList();
                r.GetProperty("teaching").GetDecimal().ShouldBe(confirmed.Sum(i => i.GetProperty("amount").GetDecimal()));
                r.GetProperty("pendingCount").GetInt32().ShouldBe(items.Count - confirmed.Count);
                var total = r.GetProperty("base").GetDecimal() + r.GetProperty("teaching").GetDecimal() + r.GetProperty("substituteBonus").GetDecimal()
                    - r.GetProperty("lateFine").GetDecimal() + r.GetProperty("adjust").GetDecimal();
                r.GetProperty("total").GetDecimal().ShouldBe(Math.Max(0, total));
                items.ShouldAllBe(i => i.GetProperty("branchId").GetGuid() == r.GetProperty("branchId").GetGuid()); // E5
                checkedRows += items.Count;
            }
        }
        checkedRows.ShouldBeGreaterThan(0, "Dữ liệu mẫu cần có buổi dạy trong 2 tháng gần nhất");
    }

    [Fact(DisplayName = "TC-13: điều chỉnh → chốt (phiếu lương email) → sửa bị chặn E2 → mở khóa → chốt lại → đã chi → không mở khóa E3")]
    public async Task Teacher_payroll_lock_cycle()
    {
        const string month = "2027-03";
        var admin = await SignIn();
        // Bật việc "phiếu lương" của tích hợp email (TH-03)
        await Ok(await admin.PutAsJsonAsync("/api/v1/system/integrations/email/events/payslip", new { on = true }, Ct));

        var board = await Ok(await admin.GetAsync($"{Finance}/payroll/teacher?month={month}&branchId={Cg}", Ct));
        board.GetProperty("locked").GetBoolean().ShouldBeFalse();
        board.GetProperty("lockDeadline").GetString().ShouldStartWith("2027-04-");
        var teacher = Rows(board).First(r => r.GetProperty("branchId").GetGuid() == Cg);
        var teacherId = teacher.GetProperty("employeeId").GetGuid();
        var before = teacher.GetProperty("total").GetDecimal();

        (await Code(await admin.PostAsJsonAsync($"{Finance}/pay-adjustments", new { month, employeeId = teacherId, amount = 0, reason = "x" }, Ct)))
            .ShouldBe("finance.pay_adjust.invalid_amount");
        (await Code(await admin.PostAsJsonAsync($"{Finance}/pay-adjustments", new { month, employeeId = teacherId, amount = 100_000, reason = " " }, Ct)))
            .ShouldBe("finance.pay_adjust.reason_required");
        var adjust = await Ok(await admin.PostAsJsonAsync($"{Finance}/pay-adjustments",
            new { month, employeeId = teacherId, amount = 500_000, reason = "Thưởng lớp đông" }, Ct));
        adjust.GetProperty("by").GetString().ShouldNotBe("—");

        board = await Ok(await admin.GetAsync($"{Finance}/payroll/teacher?month={month}&branchId={Cg}", Ct));
        Rows(board).Single(r => r.GetProperty("employeeId").GetGuid() == teacherId).GetProperty("total").GetDecimal().ShouldBe(before + 500_000);

        var sentBefore = api.Emails.Sent.Count;
        var locked = await Ok(await admin.PostAsJsonAsync($"{Finance}/payroll/teacher/{month}/lock", new { branchIds = new[] { Cg } }, Ct));
        locked.GetProperty("locks")[0].GetProperty("kind").GetString().ShouldBe("teacher");
        (await Code(await admin.PostAsJsonAsync($"{Finance}/payroll/teacher/{month}/lock", new { branchIds = new[] { Cg } }, Ct)))
            .ShouldBe("finance.payroll.locked");
        await Eventually(() => Task.FromResult(api.Emails.Sent.Skip(sentBefore).Any(m => m.Subject == "Phiếu lương tháng 03/2027")));

        // E2 — tháng đã chốt: không thêm / xóa điều chỉnh, không xác nhận buổi (Academic hỏi IPayrollLocks)
        (await Code(await admin.PostAsJsonAsync($"{Finance}/pay-adjustments",
            new { month, employeeId = teacherId, amount = 100_000, reason = "Thêm" }, Ct))).ShouldBe("finance.payroll.locked");
        (await Code(await admin.DeleteAsync($"{Finance}/pay-adjustments/{adjust.GetProperty("id").GetGuid()}", Ct))).ShouldBe("finance.payroll.locked");
        await using (var scope = api.Scope())
            (await scope.ServiceProvider.GetRequiredService<IPayrollLocks>().IsTeacherPayrollLockedAsync(Cg, new DateOnly(2027, 3, 10), Ct)).ShouldBeTrue();

        board = await Ok(await admin.GetAsync($"{Finance}/payroll/teacher?month={month}&branchId={Cg}", Ct));
        board.GetProperty("locked").GetBoolean().ShouldBeTrue();
        board.GetProperty("locks")[0].GetProperty("lockedBy").GetString().ShouldNotBe("—");

        await Ok(await admin.PostAsJsonAsync($"{Finance}/payroll/teacher/{month}/unlock", new { branchIds = new[] { Cg } }, Ct));
        await Ok(await admin.DeleteAsync($"{Finance}/pay-adjustments/{adjust.GetProperty("id").GetGuid()}", Ct));
        (await Code(await admin.PostAsJsonAsync($"{Finance}/payroll/teacher/{month}/paid", new { branchIds = new[] { Cg } }, Ct)))
            .ShouldBe("finance.payroll.not_locked");

        await Ok(await admin.PostAsJsonAsync($"{Finance}/payroll/teacher/{month}/lock", new { branchIds = new[] { Cg } }, Ct)); // chốt lại (E4)
        board = await Ok(await admin.GetAsync($"{Finance}/payroll/teacher?month={month}&branchId={Cg}", Ct));
        Rows(board).Single(r => r.GetProperty("employeeId").GetGuid() == teacherId).GetProperty("total").GetDecimal().ShouldBe(before);
        var paid = await Ok(await admin.PostAsJsonAsync($"{Finance}/payroll/teacher/{month}/paid", new { branchIds = new[] { Cg } }, Ct));
        paid.GetProperty("locks")[0].GetProperty("paidAt").ValueKind.ShouldBe(JsonValueKind.String);
        (await Code(await admin.PostAsJsonAsync($"{Finance}/payroll/teacher/{month}/unlock", new { branchIds = new[] { Cg } }, Ct)))
            .ShouldBe("finance.payroll.already_paid");
    }

    [Fact(DisplayName = "TC-14: lương NV — chưa có chấm công lấy công chuẩn (E2), thực nhận = tổng − phạt − BHXH − thuế")]
    public async Task Staff_payroll_board()
    {
        var admin = await SignIn();
        var board = await Ok(await admin.GetAsync($"{Finance}/payroll/staff?month=2027-05", Ct));
        var rows = Rows(board).ToList();
        rows.Count.ShouldBeGreaterThan(0);
        foreach (var r in rows)
        {
            r.GetProperty("hasTimesheet").GetBoolean().ShouldBeFalse();
            r.GetProperty("payDays").GetInt32().ShouldBe(26);
            var net = r.GetProperty("gross").GetDecimal() - r.GetProperty("fines").GetDecimal() - r.GetProperty("insurance").GetDecimal()
                - r.GetProperty("tax").GetDecimal();
            r.GetProperty("net").GetDecimal().ShouldBe(Math.Max(0, net));
        }
        board.GetProperty("total").GetDecimal().ShouldBe(rows.Sum(r => r.GetProperty("net").GetDecimal()));

        // Tháng hiện tại có bảng công thật → ít nhất 1 NV có chấm công
        var now = await Ok(await admin.GetAsync($"{Finance}/payroll/staff?month={DateTime.Today:yyyy-MM}", Ct));
        Rows(now).ShouldContain(r => r.GetProperty("hasTimesheet").GetBoolean());

        var sale = await SignIn("sale@jpcenter.vn");
        (await sale.GetAsync($"{Finance}/payroll/staff?month=2027-05", Ct)).StatusCode.ShouldBe(HttpStatusCode.Forbidden);
    }

    [Fact(DisplayName = "TC-12: bậc hoa hồng (E1, E2), chỉ tiêu, kết quả tháng; TC-04.E5 hóa đơn mới tự có Sale; kế toán gán lại Sale")]
    public async Task Commissions()
    {
        var admin = await SignIn();
        var original = await Ok(await admin.GetAsync($"{Finance}/commissions?month=2027-03", Ct));
        var tiers = original.GetProperty("tiers").EnumerateArray().Select(t => new { from = t.GetProperty("from").GetDecimal(), percent = t.GetProperty("percent").GetDecimal() }).ToList();
        tiers.Count.ShouldBeGreaterThan(0);

        (await Code(await admin.PutAsJsonAsync($"{Finance}/commission-tiers", new { tiers = new[] { new { from = 1_000_000, percent = 5 } } }, Ct)))
            .ShouldBe("finance.commission.zero_tier_required");
        (await Code(await admin.PutAsJsonAsync($"{Finance}/commission-tiers",
            new { tiers = new[] { new { from = 0, percent = 2 }, new { from = 0, percent = 3 } } }, Ct))).ShouldBe("finance.commission.duplicate_tier");
        (await Code(await admin.PutAsJsonAsync($"{Finance}/commission-tiers", new { tiers = new[] { new { from = 0, percent = 120 } } }, Ct)))
            .ShouldBe("finance.commission.invalid_percent");
        var saved = await Ok(await admin.PutAsJsonAsync($"{Finance}/commission-tiers", new { tiers }, Ct));
        saved.GetArrayLength().ShouldBe(tiers.Count);

        // Chỉ tiêu tháng
        var sale = original.GetProperty("results")[0];
        var saleId = sale.GetProperty("employeeId").GetGuid();
        await Ok(await admin.PutAsJsonAsync($"{Finance}/sale-targets", new { employeeId = saleId, month = "2027-03", target = 80_000_000 }, Ct));
        await Ok(await admin.PutAsJsonAsync($"{Finance}/sale-targets", new { employeeId = saleId, month = "2027-03", target = 90_000_000 }, Ct));
        (await Code(await admin.PutAsJsonAsync($"{Finance}/sale-targets", new { employeeId = saleId, month = "2027-03", target = -1 }, Ct)))
            .ShouldBe("finance.commission.invalid_target");
        var board = await Ok(await admin.GetAsync($"{Finance}/commissions?month=2027-03", Ct));
        board.GetProperty("results").EnumerateArray().Single(r => r.GetProperty("employeeId").GetGuid() == saleId)
            .GetProperty("target").GetDecimal().ShouldBe(90_000_000);

        // TC-04.E5 — hóa đơn mới ghi nhận Sale; gán lại thì ghi đè + đánh dấu gán tay
        var invoice = await NewInvoice(admin);
        invoice.GetProperty("saleId").ValueKind.ShouldBe(JsonValueKind.String);
        invoice.GetProperty("saleAssigned").GetBoolean().ShouldBeFalse();
        var other = board.GetProperty("results").EnumerateArray().Select(r => r.GetProperty("employeeId").GetGuid())
            .First(id => id != invoice.GetProperty("saleId").GetGuid());
        var id = invoice.GetProperty("id").GetGuid();
        var teacherAsSale = SeedData.IdOf("t-anh");
        (await Code(await admin.PutAsJsonAsync($"{Finance}/invoices/{id}/sale", new { employeeId = teacherAsSale }, Ct))).ShouldBe("finance.commission.not_sale");
        await Ok(await admin.PutAsJsonAsync($"{Finance}/invoices/{id}/sale", new { employeeId = other }, Ct));
        var after = await Ok(await admin.GetAsync($"{Finance}/invoices/{id}", Ct));
        (after.GetProperty("saleId").GetGuid(), after.GetProperty("saleAssigned").GetBoolean()).ShouldBe((other, true));

        var teacher = await SignIn("giaovien@jpcenter.vn");
        (await teacher.GetAsync($"{Finance}/commissions?month=2027-03", Ct)).StatusCode.ShouldBe(HttpStatusCode.Forbidden);
    }

    [Fact(DisplayName = "TC-13 bước 6: cổng GV xem lương tạm tính của chính mình; nhân viên không gọi được")]
    public async Task Teacher_portal_payroll()
    {
        var teacher = await SignIn("giaovien@jpcenter.vn");
        var mine = await Ok(await teacher.GetAsync($"{Finance}/portal/payroll?month={DateTime.Today:yyyy-MM}", Ct));
        mine.GetProperty("month").GetString().ShouldBe(DateTime.Today.ToString("yyyy-MM"));
        if (mine.GetProperty("slip").ValueKind == JsonValueKind.Object)
            mine.GetProperty("slip").GetProperty("fullName").GetString().ShouldBe("Nguyễn Thu Hà");

        var admin = await SignIn();
        (await admin.GetAsync($"{Finance}/portal/payroll?month=2027-03", Ct)).StatusCode.ShouldBe(HttpStatusCode.Forbidden);
    }

    /// <summary>HV mới ghi danh lớp N5-K21 (Cầu Giấy) → hóa đơn do Finance lập ở nền</summary>
    private async Task<JsonElement> NewInvoice(HttpClient admin)
    {
        var name = $"Học viên hoa hồng {Interlocked.Increment(ref _seq)}{Random.Shared.Next(100, 999)}";
        var created = await Ok(await admin.PostAsJsonAsync("/api/v1/students/import", new { rows = new[] { new { fullName = name, branchId = Cg } } }, Ct));
        var studentId = created.GetProperty("created")[0].GetProperty("id").GetGuid();
        await Ok(await admin.PostAsJsonAsync("/api/v1/students/enrollments",
            new { studentId, classId = ClassN5K21, amount = 6_000_000, discount = 0, dueDate = "2026-12-31" }, Ct));
        JsonElement invoice = default;
        await Eventually(async () =>
        {
            var list = await Ok(await admin.GetAsync($"{Finance}/invoices?studentId={studentId}", Ct));
            if (list.GetArrayLength() == 0) return false;
            invoice = list[0];
            return true;
        });
        return invoice;
    }
}
