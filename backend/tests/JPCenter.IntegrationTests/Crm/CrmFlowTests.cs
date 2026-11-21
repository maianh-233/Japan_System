using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text.Json;
using JPCenter.Application.Common.Messaging;
using JPCenter.Infrastructure.Common.Persistence;
using JPCenter.IntegrationTests.Infrastructure;
using JPCenter.Modules.Crm.Application.LeadRules.CheckSla;
using JPCenter.Modules.Crm.Domain.Leads;
using JPCenter.Modules.Crm.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;

namespace JPCenter.IntegrationTests.Crm;

/// <summary>User story 05 — Tuyển sinh qua API + PostgreSQL thật với dữ liệu mẫu.</summary>
[Collection(SeededCollection.Name)]
public sealed class CrmFlowTests(SeededApiFactory api)
{
    private const string Admin = "admin@jpcenter.vn", Sale = "sale@jpcenter.vn", SaleCg = "tung.tung@jpcenter.vn", Staff = "giaovu@jpcenter.vn";
    private const string Base = "/api/v1/crm";
    private static readonly Guid Cg = SeedData.IdOf("cg"), Q1 = SeedData.IdOf("q1");
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

    private static async Task<JsonElement> Expect(HttpResponseMessage r, HttpStatusCode status, string? code = null)
    {
        var body = await r.Content.ReadAsStringAsync(Ct);
        r.StatusCode.ShouldBe(status, body);
        var json = JsonDocument.Parse(body).RootElement;
        if (code is not null) json.GetProperty("code").GetString().ShouldBe(code);
        return json;
    }

    private static string Phone() => $"09{Random.Shared.Next(10_000_000, 99_999_999)}";

    private static object LeadBody(string name, Guid branch, string? phone = null, Guid? assignee = null, string source = "facebook",
        string status = "new", string? lostReason = null) =>
        new { fullName = name, phone = phone ?? Phone(), source, branchId = branch, assigneeId = assignee, status, lostReason };

    private static Guid Id(JsonElement e, string prop = "id") => e.GetProperty(prop).GetGuid();

    /// <summary>API bỏ trường null khi trả JSON</summary>
    private static bool IsNull(JsonElement e, string prop) => !e.TryGetProperty(prop, out var v) || v.ValueKind == JsonValueKind.Null;

    private async Task<JsonElement> CreateLead(HttpClient c, object body) =>
        (await Ok(await c.PostAsJsonAsync($"{Base}/leads", body, Ct))).GetProperty("lead");

    private static HttpRequestMessage Idempotent(string url, object body) => new(HttpMethod.Post, url)
    {
        Content = JsonContent.Create(body), Headers = { { "Idempotency-Key", Guid.NewGuid().ToString("N") } },
    };

    // ---------- CRM-01 → CRM-06 ----------

    [Fact(DisplayName = "CRM-01→06: Sale (own) thêm lead về mình, trùng SĐT chỉ cảnh báo, ngoài phạm vi 404, giai đoạn, tư vấn, ẩn")]
    public async Task Lead_lifecycle()
    {
        var sale = await SignIn(Sale);
        var phone = Phone();

        var created = await Ok(await sale.PostAsJsonAsync($"{Base}/leads", LeadBody("Lê Thị Thu", Q1, phone), Ct));
        var lead = created.GetProperty("lead");
        Id(lead, "assigneeId").ShouldBe(SeedData.IdOf("e-khoa"));
        lead.GetProperty("code").GetString()!.ShouldStartWith("LD");
        created.GetProperty("warnings").GetArrayLength().ShouldBe(0);
        var id = Id(lead);

        // E3: trùng SĐT (viết +84) → vẫn lưu, có cảnh báo trỏ về lead cũ
        var dup = await Ok(await sale.PostAsJsonAsync($"{Base}/leads", LeadBody("Bố Thu", Q1, "+84" + phone[1..]), Ct));
        dup.GetProperty("warnings")[0].GetProperty("code").GetString().ShouldBe("crm.lead.duplicate_phone");
        Id(dup.GetProperty("warnings")[0], "leadId").ShouldBe(id);
        (await Ok(await sale.GetAsync($"{Base}/leads/duplicates?phone={phone}", Ct))).GetArrayLength().ShouldBe(2);

        await Expect(await sale.PostAsJsonAsync($"{Base}/leads", LeadBody("X", Cg), Ct), HttpStatusCode.Forbidden, "crm.branch_not_allowed");
        await Expect(await sale.PostAsJsonAsync($"{Base}/leads", LeadBody("X", Q1, "12345"), Ct), HttpStatusCode.BadRequest, "crm.lead.invalid_phone");
        await Expect(await sale.PostAsJsonAsync($"{Base}/leads", LeadBody("X", Q1, status: "lost"), Ct), HttpStatusCode.BadRequest,
            "crm.lead.lost_reason_required");

        var list = await Ok(await sale.GetAsync($"{Base}/leads?q={phone}", Ct));
        list.GetProperty("total").GetInt32().ShouldBe(2);
        var board = await Ok(await sale.GetAsync($"{Base}/leads/board", Ct));
        board.EnumerateArray().Single(c => c.GetProperty("status").GetString() == "new").GetProperty("count").GetInt32().ShouldBeGreaterThanOrEqualTo(2);

        // E1: Sale chi nhánh khác (own) không thấy; giáo vụ không có quyền
        await Expect(await (await SignIn(SaleCg)).GetAsync($"{Base}/leads/{id}", Ct), HttpStatusCode.NotFound, "crm.lead.not_found");
        await Expect(await (await SignIn(Staff)).GetAsync($"{Base}/leads", Ct), HttpStatusCode.Forbidden);

        // CRM-04
        await Expect(await sale.PostAsJsonAsync($"{Base}/leads/{id}/status", new { status = "won" }, Ct), HttpStatusCode.Conflict,
            "crm.lead.won_requires_enrollment");
        await Expect(await sale.PostAsJsonAsync($"{Base}/leads/{id}/status", new { status = "lost" }, Ct), HttpStatusCode.BadRequest,
            "crm.lead.lost_reason_required");

        // CRM-05: tư vấn đầu tiên + hẹn test → "Hẹn test"; nội dung trống → lỗi
        await Expect(await sale.PostAsJsonAsync($"{Base}/leads/{id}/consultations", new { channel = "call", result = "interested", content = " " }, Ct),
            HttpStatusCode.BadRequest, "crm.lead.consult_content_required");
        var detail = await Ok(await sale.PostAsJsonAsync($"{Base}/leads/{id}/consultations",
            new { channel = "call", result = "test_booked", content = "Hẹn test thứ 7", nextFollowUpAt = DateTimeOffset.UtcNow.AddDays(2) }, Ct));
        detail.GetProperty("lead").GetProperty("status").GetString().ShouldBe("test_scheduled");
        detail.GetProperty("lead").GetProperty("lastContactAt").ValueKind.ShouldBe(JsonValueKind.String);
        detail.GetProperty("consultations").EnumerateArray().Count(c => !c.GetProperty("system").GetBoolean()).ShouldBe(1);

        // Mất → mở lại (dòng hệ thống)
        await Ok(await sale.PostAsJsonAsync($"{Base}/leads/{id}/status", new { status = "lost", lostReason = "Đi làm xa" }, Ct));
        var reopened = await Ok(await sale.PostAsJsonAsync($"{Base}/leads/{id}/status", new { status = "consulting" }, Ct));
        IsNull(reopened, "lostReason").ShouldBeTrue();

        // CRM-06
        (await Ok(await sale.PostAsync($"{Base}/leads/{id}/hide", null, Ct))).GetProperty("hidden").GetBoolean().ShouldBeTrue();
        await Expect(await sale.PostAsync($"{Base}/leads/{id}/hide", null, Ct), HttpStatusCode.Conflict, "crm.lead.already_hidden");
        (await Ok(await sale.GetAsync($"{Base}/leads?q={phone}", Ct))).GetProperty("total").GetInt32().ShouldBe(1);
        (await Ok(await sale.GetAsync($"{Base}/leads?q={phone}&hidden=true", Ct))).GetProperty("total").GetInt32().ShouldBe(1);
        (await Ok(await sale.PostAsync($"{Base}/leads/{id}/unhide", null, Ct))).GetProperty("hidden").GetBoolean().ShouldBeFalse();

        // Sửa đè (E5)
        var fresh = (await Ok(await sale.GetAsync($"{Base}/leads/{id}", Ct))).GetProperty("lead");
        var version = fresh.GetProperty("version").GetUInt32();
        var edit = new { fullName = "Lê Thị Thu", phone, source = "facebook", branchId = Q1, assigneeId = SeedData.IdOf("e-khoa"), version };
        await Ok(await sale.PutAsJsonAsync($"{Base}/leads/{id}", edit, Ct));
        await Expect(await sale.PutAsJsonAsync($"{Base}/leads/{id}", edit, Ct), HttpStatusCode.Conflict);
    }

    // ---------- CRM-09 → CRM-11 ----------

    [Fact(DisplayName = "CRM-10/11: lead không chọn Sale → chia theo quy tắc + nhật ký; quá 2×SLA → cảnh báo + tự chuyển 1 lần")]
    public async Task Routing_and_sla()
    {
        var admin = await SignIn(Admin);
        var lead = await CreateLead(admin, LeadBody("Đỗ Văn Khách", Cg, source: "zalo"));
        string[] team = ["e-tung", "e-bao", "e-nhu"];
        var first = Id(lead, "assigneeId");
        team.Select(SeedData.IdOf).ShouldContain(first);

        var logs = await Ok(await admin.GetAsync($"{Base}/lead-assignment-logs?leadId={Id(lead)}", Ct));
        var log = logs.GetProperty("items")[0];
        log.GetProperty("ruleName").GetString().ShouldBe("Cầu Giấy — xoay vòng trong ca");
        log.GetProperty("reason").GetString()!.ShouldStartWith("Quy tắc \"Cầu Giấy — xoay vòng trong ca\" →");

        // Lead tạo 40 phút trước (SLA quy tắc 15 phút, bật tự chuyển) → cảnh báo + chuyển Sale khác
        var leadId = new LeadId(Id(lead));
        await using (var scope = api.Scope())
        {
            var db = scope.ServiceProvider.GetRequiredService<CrmDbContext>();
            var entity = await db.Leads.FirstAsync(l => l.Id == leadId, Ct);
            db.Entry(entity).Property("CreatedAt").CurrentValue = DateTimeOffset.UtcNow.AddMinutes(-40);
            await db.SaveChangesAsync(Ct);
            (await scope.ServiceProvider.GetRequiredService<ISender>().Send(new CheckSlaCommand(), Ct)).IsSuccess.ShouldBeTrue();
        }
        var after = (await Ok(await admin.GetAsync($"{Base}/leads/{leadId.Value}", Ct))).GetProperty("lead");
        after.GetProperty("slaEscalatedAt").ValueKind.ShouldBe(JsonValueKind.String);
        after.GetProperty("slaReassignedAt").ValueKind.ShouldBe(JsonValueKind.String);
        Id(after, "assigneeId").ShouldNotBe(first);
        after.GetProperty("slaOverdue").GetBoolean().ShouldBeTrue();

        // Chạy lại: không chuyển thêm (E1, E3)
        await using (var scope = api.Scope())
            await scope.ServiceProvider.GetRequiredService<ISender>().Send(new CheckSlaCommand(), Ct);
        Id((await Ok(await admin.GetAsync($"{Base}/leads/{leadId.Value}", Ct))).GetProperty("lead"), "assigneeId").ShouldBe(Id(after, "assigneeId"));

        // Chuyển tay khi đang quá SLA
        var target = team.Select(SeedData.IdOf).First(x => x != Id(after, "assigneeId"));
        var assigned = await Ok(await admin.PostAsJsonAsync($"{Base}/leads/{leadId.Value}/assign", new { saleId = target }, Ct));
        Id(assigned, "assigneeId").ShouldBe(target);
        (await Ok(await admin.GetAsync($"{Base}/lead-assignment-logs?leadId={leadId.Value}", Ct))).GetProperty("items")[0].GetProperty("reason")
            .GetString()!.ShouldStartWith("Chuyển tay do quá SLA");
        await Expect(await admin.PostAsJsonAsync($"{Base}/leads/{leadId.Value}/assign", new { saleId = SeedData.IdOf("e-khoa") }, Ct),
            HttpStatusCode.BadRequest, "crm.lead.invalid_assignee");
    }

    [Fact(DisplayName = "CRM-09: quy tắc chia lead — kiểm tra, thêm cuối danh sách, đổi thứ tự 1 request, xóa; không quyền → 403")]
    public async Task Lead_rules()
    {
        var admin = await SignIn(Admin);
        var body = new { name = "Thử", active = false, method = "least_load", saleIds = new[] { SeedData.IdOf("e-khoa") }, slaMinutes = 0 };
        await Expect(await admin.PostAsJsonAsync($"{Base}/lead-rules", body, Ct), HttpStatusCode.BadRequest, "crm.lead_rule.invalid_sla");
        await Expect(await admin.PostAsJsonAsync($"{Base}/lead-rules", body with { slaMinutes = 30, saleIds = Array.Empty<Guid>() }, Ct),
            HttpStatusCode.BadRequest, "crm.lead_rule.no_sale");

        var rule = await Ok(await admin.PostAsJsonAsync($"{Base}/lead-rules", body with { slaMinutes = 30 }, Ct));
        var all = await Ok(await admin.GetAsync($"{Base}/lead-rules", Ct));
        Id(all[all.GetArrayLength() - 1]).ShouldBe(Id(rule));

        var reversed = all.EnumerateArray().Select(r => Id(r)).Reverse().ToList();
        var ordered = await Ok(await admin.PutAsJsonAsync($"{Base}/lead-rules/order", new { ids = reversed }, Ct));
        Id(ordered[0]).ShouldBe(Id(rule));
        await Expect(await admin.PutAsJsonAsync($"{Base}/lead-rules/order", new { ids = reversed.Take(2) }, Ct), HttpStatusCode.BadRequest,
            "crm.lead_rule.invalid_order");
        await Ok(await admin.PutAsJsonAsync($"{Base}/lead-rules/order", new { ids = reversed.AsEnumerable().Reverse() }, Ct));

        (await admin.DeleteAsync($"{Base}/lead-rules/{Id(rule)}", Ct)).IsSuccessStatusCode.ShouldBeTrue();
        await Expect(await (await SignIn(Sale)).GetAsync($"{Base}/lead-rules", Ct), HttpStatusCode.Forbidden);
    }

    // ---------- CRM-07, CRM-08 ----------

    [Fact(DisplayName = "CRM-07/08: chốt lead → HV + ghi danh, 1 hồ sơ bản thân, HV nhỏ tuổi PH quyết định, đổi liên hệ chính")]
    public async Task Enroll_and_links()
    {
        var admin = await SignIn(Admin);
        var lead = await CreateLead(admin, LeadBody("Phạm Minh Đức", Cg, assignee: SeedData.IdOf("e-tung")));
        var url = $"{Base}/leads/{Id(lead)}/enroll";

        // Chỉ tạo hồ sơ (E8) — lead vẫn "Đã chốt"
        var self = await Ok(await admin.SendAsync(Idempotent(url, new { who = "self", discount = 0 }), Ct));
        self.GetProperty("studentCreated").GetBoolean().ShouldBeTrue();
        IsNull(self, "enrollmentId").ShouldBeTrue();
        self.GetProperty("lead").GetProperty("lead").GetProperty("status").GetString().ShouldBe("won");
        await Expect(await admin.SendAsync(Idempotent(url, new { who = "self", discount = 0 }), Ct), HttpStatusCode.Conflict,
            "crm.lead.self_already_linked");

        // Người khác (con 10 tuổi) + lớp chờ khai giảng của chi nhánh → ghi danh, PH là người quyết định (E9)
        var child = new { fullName = "Phạm Minh Khôi", dob = DateOnly.FromDateTime(DateTime.Today.AddYears(-10)), relationship = "child" };
        await Expect(await admin.SendAsync(Idempotent(url, new { who = "other", student = child, classId = SeedData.IdOf("C-N3K13"), discount = 0 }), Ct),
            HttpStatusCode.BadRequest, "crm.lead.class_other_branch");
        await Expect(await admin.SendAsync(Idempotent(url,
                new { who = "other", student = child, classId = SeedData.IdOf("C-N5K21"), discount = 999_999_999 }), Ct),
            HttpStatusCode.BadRequest, "finance.invoice.discount_too_large");
        var enrolled = await Ok(await admin.SendAsync(Idempotent(url,
            new { who = "other", student = child, classId = SeedData.IdOf("C-N5K21"), discount = 100_000 }), Ct));
        enrolled.GetProperty("enrollmentId").ValueKind.ShouldBe(JsonValueKind.String);
        enrolled.GetProperty("classCode").GetString().ShouldBe("GVV_N5-K21");
        var links = enrolled.GetProperty("lead").GetProperty("students");
        links.GetArrayLength().ShouldBe(2);
        var kid = links.EnumerateArray().Single(k => k.GetProperty("relationship").GetString() == "child");
        (kid.GetProperty("primary").GetBoolean(), kid.GetProperty("billing").GetBoolean()).ShouldBe((true, true));
        enrolled.GetProperty("lead").GetProperty("consultations").EnumerateArray()
            .Any(c => c.GetProperty("content").GetString()!.StartsWith("Ghi danh Phạm Minh Khôi")).ShouldBeTrue();

        // Bắt buộc Idempotency-Key (E10)
        await Expect(await admin.PostAsJsonAsync(url, new { who = "self", discount = 0 }, Ct), HttpStatusCode.BadRequest);

        // CRM-08.E2: mẹ (L-0002) thành liên hệ chính của An → bố (L-0001) tự tắt; khôi phục lại cuối test
        var mother = SeedData.IdOf("L-0002");
        var father = SeedData.IdOf("L-0001");
        var an = SeedData.IdOf("ST-1");
        var swap = await Ok(await admin.PutAsJsonAsync($"{Base}/leads/{mother}/students/{an}",
            new { relationship = "child", primary = true, billing = false, notify = true }, Ct));
        swap.GetProperty("warnings").EnumerateArray().Select(w => w.GetProperty("code").GetString()).ShouldContain("crm.lead.primary_changed");
        var fatherLinks = (await Ok(await admin.GetAsync($"{Base}/leads/{father}", Ct))).GetProperty("students");
        fatherLinks.EnumerateArray().Single(k => Id(k, "studentId") == an).GetProperty("primary").GetBoolean().ShouldBeFalse();
        await Ok(await admin.PutAsJsonAsync($"{Base}/leads/{father}/students/{an}",
            new { relationship = "child", primary = true, billing = true, notify = true }, Ct));

        // Liên kết HV có sẵn / gỡ liên kết
        await Expect(await admin.PostAsJsonAsync($"{Base}/leads/{father}/students",
            new { studentId = an, relationship = "child", primary = false, billing = false, notify = false }, Ct), HttpStatusCode.Conflict,
            "crm.lead.already_linked");
        var linked = await Ok(await admin.PostAsJsonAsync($"{Base}/leads/{Id(lead)}/students",
            new { studentId = an, relationship = "other", primary = false, billing = false, notify = false }, Ct));
        linked.GetProperty("students").GetArrayLength().ShouldBe(3);
        (await Ok(await admin.DeleteAsync($"{Base}/leads/{Id(lead)}/students/{an}", Ct))).GetProperty("students").GetArrayLength().ShouldBe(2);

        // Sale (own) chốt lead của mình: tạo hồ sơ với quyền students.profiles.create (own)
        var sale = await SignIn(Sale);
        var mine = await CreateLead(sale, LeadBody("Trịnh Văn Tài", Q1));
        (await Ok(await sale.SendAsync(Idempotent($"{Base}/leads/{Id(mine)}/enroll", new { who = "self", discount = 0 }), Ct)))
            .GetProperty("studentCode").GetString()!.ShouldStartWith("HV");
    }

    // ---------- CRM-12, CRM-13 ----------

    [Fact(DisplayName = "CRM-12/13: soạn bài test bốc câu đã duyệt, gửi link, lead làm bài công khai không thấy đáp án, ghi kết quả")]
    public async Task Placement_test()
    {
        var admin = await SignIn(Admin);
        var bands = new[] { new { minPct = 0, level = "Sơ cấp", course = "n5" }, new { minPct = 80, level = "N5", course = "n4" } };
        var sections = new[] { new { level = "N5", skill = "vocab", count = 2 } };
        await Expect(await admin.PostAsJsonAsync($"{Base}/placement-tests",
            new { name = "T", sections, durationMin = 10, bands = new[] { new { minPct = 50, level = "A", course = "" } } }, Ct),
            HttpStatusCode.BadRequest, "crm.placement_test.band_zero_required");
        await Expect(await admin.PostAsJsonAsync($"{Base}/placement-tests",
            new { name = "T", sections = new[] { new { level = "N1", skill = "listening", count = 500 } }, durationMin = 10, bands }, Ct),
            HttpStatusCode.Conflict, "crm.placement_test.no_question");

        var test = await Ok(await admin.PostAsJsonAsync($"{Base}/placement-tests", new { name = "Test nhanh", sections, durationMin = 10, bands }, Ct));
        test.GetProperty("status").GetString().ShouldBe("draft");
        test.GetProperty("questionIds").GetArrayLength().ShouldBe(2);
        (await Ok(await admin.GetAsync($"{Base}/placement-tests/{Id(test)}/questions", Ct)))[0].GetProperty("answer").ValueKind
            .ShouldBe(JsonValueKind.Number);

        var sale = await SignIn(Sale);
        var lead = await CreateLead(sale, LeadBody("Ngô Thanh Hà", Q1));
        var send = new { testId = Id(test), leadId = Id(lead), channel = "zalo", validDays = 2 };
        await Expect(await sale.PostAsJsonAsync($"{Base}/placement-attempts", send, Ct), HttpStatusCode.Conflict, "crm.placement_test.not_active");
        await Ok(await admin.PostAsJsonAsync($"{Base}/placement-tests/{Id(test)}/status", new { status = "active" }, Ct));
        var attempt = await Ok(await sale.PostAsJsonAsync($"{Base}/placement-attempts", send, Ct));
        var token = attempt.GetProperty("token").GetString()!;
        token.Length.ShouldBeGreaterThanOrEqualTo(40);

        // Trang công khai (không đăng nhập)
        var anon = api.CreateClient();
        var pub = $"{Base}/public/placement/{token}";
        (await Ok(await anon.GetAsync(pub, Ct))).GetProperty("status").GetString().ShouldBe("sent");
        await Expect(await anon.GetAsync($"{Base}/public/placement/{new string('x', 43)}", Ct), HttpStatusCode.NotFound);
        var started = await Ok(await anon.PostAsync($"{pub}/start", null, Ct));
        var questions = started.GetProperty("questions");
        questions.GetArrayLength().ShouldBe(2);
        questions[0].TryGetProperty("answer", out _).ShouldBeFalse();
        await Expect(await anon.PostAsync($"{pub}/start", null, Ct), HttpStatusCode.Conflict, "crm.placement_attempt.started");
        (await anon.PutAsJsonAsync($"{pub}/answers", new { answers = new Dictionary<Guid, int> { [Id(questions[0])] = 0 } }, Ct))
            .IsSuccessStatusCode.ShouldBeTrue();
        var done = await Ok(await anon.PostAsJsonAsync($"{pub}/submit", new { answers = new Dictionary<Guid, int> { [Id(questions[0])] = 0 } }, Ct));
        (done.GetProperty("status").GetString(), done.GetProperty("total").GetInt32()).ShouldBe(("done", 2));
        await Expect(await anon.PostAsJsonAsync($"{pub}/submit", new { answers = new { } }, Ct), HttpStatusCode.Conflict,
            "crm.placement_attempt.not_in_progress");

        // Sale ghi vào lịch sử tư vấn (1 lần)
        var logged = await Ok(await sale.PostAsync($"{Base}/placement-attempts/{Id(attempt)}/log", null, Ct));
        logged.GetProperty("logged").GetBoolean().ShouldBeTrue();
        await Expect(await sale.PostAsync($"{Base}/placement-attempts/{Id(attempt)}/log", null, Ct), HttpStatusCode.Conflict,
            "crm.placement_attempt.already_logged");
        var after = await Ok(await sale.GetAsync($"{Base}/leads/{Id(lead)}", Ct));
        after.GetProperty("consultations").EnumerateArray().Any(c => c.GetProperty("content").GetString()!.StartsWith("Kết quả test")).ShouldBeTrue();

        // Đã có người làm → không đổi đề (E6), vẫn sửa tên / thang được
        await Expect(await admin.PutAsJsonAsync($"{Base}/placement-tests/{Id(test)}",
            new { name = "Test nhanh", sections, durationMin = 15, bands }, Ct), HttpStatusCode.Conflict, "crm.placement_test.has_attempts");
        (await Ok(await admin.PutAsJsonAsync($"{Base}/placement-tests/{Id(test)}", new { name = "Test nhanh v2", sections, durationMin = 10, bands }, Ct)))
            .GetProperty("name").GetString().ShouldBe("Test nhanh v2");
    }

    // ---------- CRM-14, CRM-15 ----------

    [Fact(DisplayName = "CRM-14/15: đặt lịch hẹn, trùng người tiếp → conflict theo loại, không đến trước giờ bị chặn, đã đến cần kết quả")]
    public async Task Appointments()
    {
        var admin = await SignIn(Admin);
        var lead = await CreateLead(admin, LeadBody("Vũ Hải Yến", Cg, assignee: SeedData.IdOf("e-bao")));
        var other = await CreateLead(admin, LeadBody("Bùi Văn Lâm", Cg, assignee: SeedData.IdOf("e-bao")));
        var start = DateTimeOffset.UtcNow.Date.AddDays(3 + Random.Shared.Next(0, 300)).AddHours(3); // 10:00 giờ VN
        var staff = SeedData.IdOf("e-hang");

        var appt = await Ok(await admin.PostAsJsonAsync($"{Base}/appointments",
            new { leadId = Id(lead), kind = "test", staffId = staff, start = new DateTimeOffset(start.AddHours(7), TimeSpan.FromHours(7)) } /* giờ VN — lưu UTC */, Ct));
        appt.GetProperty("code").GetString()!.ShouldStartWith("LH");
        (await Ok(await admin.GetAsync($"{Base}/leads/{Id(lead)}", Ct))).GetProperty("lead").GetProperty("status").GetString().ShouldBe("test_scheduled");

        var clash = new { leadId = Id(other), kind = "consult", staffId = staff, start = new DateTimeOffset(start.AddMinutes(30), TimeSpan.Zero) };
        var error = await Expect(await admin.PostAsJsonAsync($"{Base}/appointments", clash, Ct), HttpStatusCode.Conflict, "crm.appointment.conflict");
        error.GetProperty("errors").GetProperty("staff")[0].GetString()!.ShouldContain(appt.GetProperty("code").GetString()!);
        (await Ok(await admin.PostAsJsonAsync($"{Base}/appointments/check", clash, Ct))).GetArrayLength().ShouldBeGreaterThan(0);

        var id = Id(appt);
        await Expect(await admin.PostAsJsonAsync($"{Base}/appointments/{id}/no-show", new { }, Ct), HttpStatusCode.Conflict, "crm.appointment.not_started");
        await Expect(await admin.PostAsJsonAsync($"{Base}/appointments/{id}/done", new { text = " " }, Ct), HttpStatusCode.BadRequest,
            "crm.appointment.outcome_required");
        (await Ok(await admin.PostAsJsonAsync($"{Base}/appointments/{id}/done", new { text = "Khách làm test, đạt N5" }, Ct)))
            .GetProperty("status").GetString().ShouldBe("done");
        await Expect(await admin.PostAsJsonAsync($"{Base}/appointments/{id}/cancel", new { text = "x" }, Ct), HttpStatusCode.Conflict,
            "crm.appointment.not_scheduled");

        // Lịch đã đến không còn chặn giờ đó
        (await Ok(await admin.PostAsJsonAsync($"{Base}/appointments/check", clash, Ct))).GetArrayLength().ShouldBe(0);
        var from = Uri.EscapeDataString(start.AddDays(-1).ToString("O"));
        (await Ok(await admin.GetAsync($"{Base}/appointments?leadId={Id(lead)}&from={from}", Ct))).GetArrayLength().ShouldBe(1);
    }

    // ---------- CRM-16 → CRM-19 ----------

    [Fact(DisplayName = "CRM-16→19: chiến dịch (trùng mã, số liệu, trạng thái), mẫu tin + gửi tin chờ tích hợp, phễu, lên lớp phía Sale")]
    public async Task Campaigns_messages_funnel()
    {
        var admin = await SignIn(Admin);
        var code = $"T{Random.Shared.Next(100000, 999999)}";
        var body = new
        {
            code, name = "Chiến dịch thử", channel = "tiktok", branchIds = new[] { Cg }, startDate = "2026-11-01", endDate = "2026-12-31",
            budget = 2_000_000, spent = 500_000, targetLeads = 10, ownerId = SeedData.IdOf("e-baoanh"), utm = $"utm_{code}", courseIds = Array.Empty<Guid>(),
        };
        await Expect(await admin.PostAsJsonAsync($"{Base}/campaigns", body with { endDate = "2026-10-01" }, Ct), HttpStatusCode.BadRequest,
            "crm.campaign.invalid_dates");
        var campaign = await Ok(await admin.PostAsJsonAsync($"{Base}/campaigns", body, Ct));
        await Expect(await admin.PostAsJsonAsync($"{Base}/campaigns", body with { utm = "khac" }, Ct), HttpStatusCode.Conflict,
            "crm.campaign.duplicate_code");
        IsNull(campaign.GetProperty("stats"), "costPerLead").ShouldBeTrue();

        await CreateLead(admin, new { fullName = "Lead TikTok", phone = Phone(), source = "tiktok", branchId = Cg, campaignId = Id(campaign) });
        await Expect(await admin.PostAsJsonAsync($"{Base}/leads",
            new { fullName = "Sai kênh", phone = Phone(), source = "zalo", branchId = Cg, campaignId = Id(campaign) }, Ct), HttpStatusCode.BadRequest,
            "crm.lead.campaign_mismatch");
        var listed = (await Ok(await admin.GetAsync($"{Base}/campaigns", Ct))).EnumerateArray().Single(c => Id(c) == Id(campaign));
        (listed.GetProperty("stats").GetProperty("leads").GetInt32(), listed.GetProperty("stats").GetProperty("costPerLead").GetDecimal())
            .ShouldBe((1, 500_000m));
        (await Ok(await admin.PostAsJsonAsync($"{Base}/campaigns/{Id(campaign)}/status", new { status = "running" }, Ct)))
            .GetProperty("status").GetString().ShouldBe("running");
        await Expect(await admin.PostAsJsonAsync($"{Base}/campaigns/{Id(campaign)}/status", new { status = "running" }, Ct), HttpStatusCode.Conflict,
            "crm.campaign.invalid_status");

        // CRM-17
        var sale = await SignIn(Sale);
        (await Ok(await sale.GetAsync($"{Base}/message-templates", Ct))).GetArrayLength().ShouldBeGreaterThanOrEqualTo(9);
        await Expect(await admin.PostAsJsonAsync($"{Base}/message-templates", new { scenario = "custom", name = "Rỗng", channel = "zalo", body = "" }, Ct),
            HttpStatusCode.BadRequest, "crm.message_template.body_required");
        var lead = await CreateLead(sale, LeadBody("Hoàng Thị Mơ", Q1));
        var sent = await Ok(await sale.PostAsJsonAsync($"{Base}/messages",
            new { channel = "zalo", scenario = "welcome", leadId = Id(lead), body = "Chào Mơ, JP Center xin chào!" }, Ct));
        (sent.GetProperty("status").GetString(), sent.GetProperty("auto").GetBoolean()).ShouldBe(("pending", false));
        (await Ok(await sale.GetAsync($"{Base}/messages?leadId={Id(lead)}", Ct))).GetArrayLength().ShouldBe(1);

        // CRM-18 (own = lead của mình) — CRM-19 phía Sale
        var funnel = await Ok(await sale.GetAsync($"{Base}/funnel", Ct));
        funnel.EnumerateArray().ShouldAllBe(j => Id(j, "assigneeId") == SeedData.IdOf("e-khoa"));
        funnel.EnumerateArray().Any(j => Id(j, "leadId") == Id(lead)).ShouldBeTrue();
        (await Ok(await sale.GetAsync("/api/v1/academic/promotions/sale", Ct))).ValueKind.ShouldBe(JsonValueKind.Array);
        await Expect(await (await SignIn(Staff)).GetAsync($"{Base}/funnel", Ct), HttpStatusCode.Forbidden);
    }
}
