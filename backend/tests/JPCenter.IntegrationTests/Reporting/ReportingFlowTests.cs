using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text.Json;
using JPCenter.Infrastructure.Common.Persistence;
using JPCenter.IntegrationTests.Infrastructure;
using JPCenter.Modules.System.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;

namespace JPCenter.IntegrationTests.Reporting;

/// <summary>User story 17 (giai đoạn 24 phase 3) — báo cáo + bảng điều khiển, chỉ đọc qua Contracts (ADR 0004), trên dữ liệu mẫu.</summary>
[Collection(SeededCollection.Name)]
public sealed class ReportingFlowTests(SeededApiFactory api)
{
    private const string Base = "/api/v1/reporting";
    private const string Admin = "admin@jpcenter.vn", GiaoVu = "giaovu@jpcenter.vn", QuanLyCg = "quanly.caugiay@jpcenter.vn", KeToan = "ketoan@jpcenter.vn",
        Sale = "tung.tung@jpcenter.vn";
    private static CancellationToken Ct => TestContext.Current.CancellationToken;

    private static readonly Guid Cg = SeedData.IdOf("cg");
    private static readonly Guid Hc = SeedData.IdOf("hc");
    private static readonly string[] Tones = ["danger", "warning", "info"];

    private async Task<HttpClient> SignIn(string email)
    {
        var client = api.CreateClient();
        var r = await client.PostAsJsonAsync("/api/v1/system/auth/login", new { identifier = email, password = SeededApiFactory.Password, remember = false }, Ct);
        r.StatusCode.ShouldBe(HttpStatusCode.OK, await r.Content.ReadAsStringAsync(Ct));
        var token = JsonDocument.Parse(await r.Content.ReadAsStringAsync(Ct)).RootElement.GetProperty("accessToken").GetString();
        client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", token);
        return client;
    }

    private static async Task<JsonElement> Ok(HttpResponseMessage r)
    {
        var body = await r.Content.ReadAsStringAsync(Ct);
        r.IsSuccessStatusCode.ShouldBeTrue(body);
        return JsonDocument.Parse(body).RootElement;
    }

    private static async Task Expect(HttpResponseMessage r, HttpStatusCode status, string? code = null)
    {
        var body = await r.Content.ReadAsStringAsync(Ct);
        r.StatusCode.ShouldBe(status, body);
        if (code is not null) JsonDocument.Parse(body).RootElement.GetProperty("code").GetString().ShouldBe(code);
    }

    private static decimal Dec(JsonElement e, string name) => e.TryGetProperty(name, out var v) && v.ValueKind == JsonValueKind.Number ? v.GetDecimal() : 0;
    private static int Int(JsonElement e, string name) => e.GetProperty(name).GetInt32();
    private static IEnumerable<JsonElement> Arr(JsonElement e, string name) => e.GetProperty(name).EnumerateArray();

    [Fact(DisplayName = "BC-03 → 06: báo cáo 6 tháng toàn hệ thống — các con số khớp nhau giữa các tab")]
    public async Task Report_all_branches_is_consistent()
    {
        var admin = await SignIn(Admin);
        var r = await Ok(await admin.GetAsync($"{Base}/reports?months=6", Ct));

        Int(r, "months").ShouldBe(6);
        Arr(r, "monthKeys").Count().ShouldBe(6);
        Arr(r, "monthKeys").Last().GetString().ShouldBe(DateTime.UtcNow.AddHours(7).ToString("yyyy-MM"));

        // Tuyển sinh
        var lead = r.GetProperty("lead");
        Arr(lead, "funnel").Select(f => f.GetProperty("key").GetString()).ShouldBe(["new", "consulting", "test_scheduled", "won", "lost"]);
        Arr(lead, "funnel").Sum(f => Int(f, "value")).ShouldBe(Int(lead, "total"));
        Arr(lead, "byMonth").Sum(p => Dec(p, "value")).ShouldBe(Int(lead, "total"));
        Arr(lead, "bySource").Sum(s => Int(s, "leads")).ShouldBe(Int(lead, "total"));
        Int(lead, "total").ShouldBeGreaterThan(0, "dữ liệu mẫu có lead trong 6 tháng");
        var cpa = Arr(lead, "byCampaign").Where(c => c.TryGetProperty("cpa", out var v) && v.ValueKind == JsonValueKind.Number).Select(c => Dec(c, "cpa")).ToList();
        cpa.ShouldBe(cpa.Order().ToList(), "chiến dịch rẻ nhất / HV chốt lên đầu");

        // Doanh thu & công nợ
        var money = r.GetProperty("money");
        Arr(money, "revenue").Sum(p => Dec(p, "value")).ShouldBe(Dec(money, "collected"));
        Arr(money, "revenue").Last().GetProperty("value").GetDecimal().ShouldBe(Dec(money, "thisMonth"));
        Arr(money, "aging").Select(a => a.GetProperty("key").GetString()).ShouldBe(["not_due", "d30", "d60", "d60p"]);
        Arr(money, "aging").Sum(a => Dec(a, "amount")).ShouldBe(Dec(money, "debt"));
        Dec(money, "overdue").ShouldBeLessThanOrEqualTo(Dec(money, "debt"));

        // Đào tạo
        var study = r.GetProperty("study");
        Int(study, "studying").ShouldBeGreaterThan(0);
        var fill = Arr(study, "fill").Select(f => Int(f, "rate")).ToList();
        fill.ShouldBe(fill.Order().ToList(), "lớp vắng nhất lên đầu");
        (Int(study, "ongoing") + Int(study, "enrolling")).ShouldBe(fill.Count);
        Arr(study, "attendanceByMonth").Count().ShouldBe(6);
        var rooms = Arr(study, "roomUse").Select(x => Int(x, "rate")).ToList();
        rooms.ShouldBe(rooms.OrderDescending().ToList());

        // So sánh chi nhánh — cộng các chi nhánh = toàn hệ thống
        var branches = Arr(r, "branches").ToList();
        branches.Count.ShouldBeGreaterThan(1);
        branches.Sum(b => Int(b, "leads")).ShouldBe(Int(lead, "total"));
        branches.Sum(b => Dec(b, "collected")).ShouldBe(Dec(money, "collected"));
        branches.Sum(b => Dec(b, "debt")).ShouldBe(Dec(money, "debt"));
        branches.Sum(b => Int(b, "classes")).ShouldBe(fill.Count);
        foreach (var b in branches)
        {
            var low = b.TryGetProperty("attendance", out var a) && a.ValueKind == JsonValueKind.Number && a.GetInt32() < 85;
            b.GetProperty("needsAttention").GetBoolean().ShouldBe(low || Dec(b, "overdue") > 0);
        }
    }

    [Fact(DisplayName = "BC-03.E3 / quy tắc 3: kỳ sai → 400; phạm vi chi nhánh chọn chi nhánh khác → chi nhánh của mình")]
    public async Task Period_and_branch_scope()
    {
        var admin = await SignIn(Admin);
        await Expect(await admin.GetAsync($"{Base}/reports?months=5", Ct), HttpStatusCode.BadRequest, "reporting.period.invalid");

        var hc = await Ok(await admin.GetAsync($"{Base}/reports?months=3&branchId={Hc}", Ct));
        hc.GetProperty("branchId").GetGuid().ShouldBe(Hc);
        Arr(hc, "branches").Select(b => b.GetProperty("key").GetGuid()).ShouldBe([Hc]);

        var manager = await SignIn(QuanLyCg);
        var own = await Ok(await manager.GetAsync($"{Base}/reports?months=3&branchId={Hc}", Ct));
        own.GetProperty("branchId").GetGuid().ShouldBe(Cg);
        Arr(own, "branches").Select(b => b.GetProperty("key").GetGuid()).ShouldBe([Cg]);
        var cgFromAdmin = await Ok(await admin.GetAsync($"{Base}/reports?months=3&branchId={Cg}", Ct));
        Dec(own.GetProperty("money"), "debt").ShouldBe(Dec(cgFromAdmin.GetProperty("money"), "debt"));
    }

    [Fact(DisplayName = "BC-07: xuất báo cáo ghi nhật ký; Giáo vụ chỉ có Bảng điều khiển → không xem / xuất báo cáo")]
    public async Task Export_writes_audit_and_requires_permission()
    {
        var giaoVu = await SignIn(GiaoVu);
        await Expect(await giaoVu.GetAsync($"{Base}/reports", Ct), HttpStatusCode.Forbidden);
        await Expect(await giaoVu.PostAsJsonAsync($"{Base}/reports/export", new { months = 6 }, Ct), HttpStatusCode.Forbidden);
        (await giaoVu.GetAsync($"{Base}/dashboard", Ct)).StatusCode.ShouldBe(HttpStatusCode.OK);

        var keToan = await SignIn(KeToan);
        var started = DateTimeOffset.UtcNow;
        var r = await Ok(await keToan.PostAsJsonAsync($"{Base}/reports/export", new { branchId = Cg, months = 12 }, Ct));
        Arr(r, "monthKeys").Count().ShouldBe(12);

        using var scope = api.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<SystemDbContext>();
        var logs = await db.AuditLogs.AsNoTracking().Where(a => a.At >= started && a.Action == "Xuất báo cáo").ToListAsync(Ct);
        logs.ShouldContain(a => a.Target.StartsWith("Báo cáo 12 tháng · "));
    }

    [Fact(DisplayName = "BC-01: bảng điều khiển khớp báo cáo; cảnh báo đúng loại / mức")]
    public async Task Dashboard_matches_report()
    {
        var admin = await SignIn(Admin);
        var d = await Ok(await admin.GetAsync($"{Base}/dashboard?branchId={Cg}", Ct));
        var r = await Ok(await admin.GetAsync($"{Base}/reports?months=3&branchId={Cg}", Ct));

        Int(d, "studying").ShouldBe(Int(r.GetProperty("study"), "studying"));
        Int(d, "ongoingClasses").ShouldBe(Int(r.GetProperty("study"), "ongoing"));
        Dec(d, "collectedThisMonth").ShouldBe(Dec(r.GetProperty("money"), "thisMonth"));
        Dec(d, "collectedPrevMonth").ShouldBe(Dec(r.GetProperty("money"), "prevMonth"));
        Dec(d, "overdue").ShouldBe(Dec(r.GetProperty("money"), "overdue"));
        Int(d, "leadsThisMonth").ShouldBe((int)Dec(Arr(r.GetProperty("lead"), "byMonth").Last(), "value"));

        string[] kinds = ["lead_sla_overdue", "unconfirmed_sessions", "missing_teacher_logs", "low_fill_classes", "integration_error"];
        foreach (var a in Arr(d, "alerts"))
        {
            kinds.ShouldContain(a.GetProperty("kind").GetString());
            a.GetProperty("tone").GetString().ShouldBeOneOf("danger", "warning");
            a.GetProperty("path").GetString()!.ShouldStartWith("/admin/");
        }
    }

    [Fact(DisplayName = "BC-02: Việc hôm nay theo quyền — Admin đủ 3 tab, Kế toán chỉ tab Kế toán, Sale (own) chỉ lead của mình")]
    public async Task Today_work_by_permission()
    {
        var admin = await SignIn(Admin);
        var boards = (await Ok(await admin.GetAsync($"{Base}/today-work", Ct))).EnumerateArray().ToList();
        boards.Select(b => b.GetProperty("role").GetString()).ShouldBe(["sale", "academic", "finance"]);
        foreach (var b in boards)
        {
            var groups = Arr(b, "groups").ToList();
            Int(b, "count").ShouldBe(groups.Sum(g => g.GetProperty("items").GetArrayLength()));
            foreach (var g in groups)
            {
                var tones = Arr(g, "items").Select(i => Array.IndexOf(Tones, i.GetProperty("tone").GetString())).ToList();
                tones.ShouldAllBe(t => t >= 0);
                tones.ShouldBe(tones.Order().ToList(), "đỏ → vàng → xanh");
            }
        }
        Arr(boards[2], "groups").Select(g => g.GetProperty("key").GetString()).ShouldBe(["due", "txn"]);

        var keToan = await SignIn(KeToan);
        var kt = (await Ok(await keToan.GetAsync($"{Base}/today-work", Ct))).EnumerateArray().Select(b => b.GetProperty("role").GetString()).ToList();
        kt.ShouldNotContain("sale");
        kt.ShouldContain("finance");

        var sale = await SignIn(Sale);
        var saleBoards = (await Ok(await sale.GetAsync($"{Base}/today-work", Ct))).EnumerateArray().ToList();
        var mine = saleBoards.Single(b => b.GetProperty("role").GetString() == "sale");
        Int(mine, "count").ShouldBeLessThanOrEqualTo(Int(boards[0], "count"));
        saleBoards.ShouldNotContain(b => b.GetProperty("role").GetString() == "finance");
    }
}
