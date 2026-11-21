using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text.Json;
using JPCenter.Infrastructure.Common.Persistence;
using JPCenter.IntegrationTests.Infrastructure;

namespace JPCenter.IntegrationTests.Crm;

/// <summary>HV-12 — Phụ huynh / người giám hộ qua <c>/api/v1/crm/guardians</c> (quyền students.contacts, giáo vụ không có quyền Tuyển sinh).</summary>
[Collection(SeededCollection.Name)]
public sealed class GuardianFlowTests(SeededApiFactory api)
{
    private const string Admin = "admin@jpcenter.vn", Sale = "sale@jpcenter.vn", Staff = "giaovu@jpcenter.vn";
    private const string Base = "/api/v1/crm/guardians";
    private static readonly Guid Cg = SeedData.IdOf("cg");
    private static CancellationToken Ct => TestContext.Current.CancellationToken;

    private async Task<HttpClient> SignIn(string email)
    {
        var client = api.CreateClient();
        var r = await client.PostAsJsonAsync("/api/v1/system/auth/login",
            new { identifier = email, password = SeededApiFactory.Password, remember = false }, Ct);
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

    private static string Phone() => $"09{Random.Shared.Next(10_000_000, 99_999_999)}";

    private static object Body(string phone, params string[] students) => new
    {
        fullName = "Ngô Thị Lan", phone, email = (string?)null, branchId = Cg, notifyChannels = new[] { "zalo" }, note = (string?)null,
        relationship = "other", studentIds = students.Select(SeedData.IdOf).ToArray(), primary = false,
    };

    private static int Links(JsonElement guardian) => guardian.GetProperty("students").GetArrayLength();

    [Fact(DisplayName = "HV-12: giáo vụ thêm phụ huynh (Đã chốt), trùng SĐT dùng lại người cũ, sửa liên hệ, cờ liên kết, gỡ liên kết")]
    public async Task Guardian_lifecycle()
    {
        var staff = await SignIn(Staff);
        // Giáo vụ không có quyền Tuyển sinh nhưng quản lý được phụ huynh
        await Expect(await staff.GetAsync("/api/v1/crm/leads", Ct), HttpStatusCode.Forbidden);
        var list = await Ok(await staff.GetAsync(Base, Ct));
        list.EnumerateArray().ShouldAllBe(g => g.GetProperty("students").EnumerateArray().Any(k => k.GetProperty("relationship").GetString() != "self"));

        // Thêm mới → lead "Đã chốt", nguồn Giới thiệu, nhận thông báo
        var phone = Phone();
        var created = await Ok(await staff.PostAsJsonAsync(Base, Body(phone, "ST-5"), Ct));
        created.GetProperty("reused").GetBoolean().ShouldBeFalse();
        var g = created.GetProperty("guardian");
        (g.GetProperty("status").GetString(), g.GetProperty("source").GetString()).ShouldBe(("won", "referral"));
        g.GetProperty("students")[0].GetProperty("notify").GetBoolean().ShouldBeTrue();
        var id = g.GetProperty("id").GetGuid();

        // E1 — cùng SĐT (dạng +84) → liên kết thêm HV vào người cũ
        var again = await Ok(await staff.PostAsJsonAsync(Base, Body("+84" + phone[1..], "ST-9"), Ct));
        again.GetProperty("reused").GetBoolean().ShouldBeTrue();
        again.GetProperty("guardian").GetProperty("id").GetGuid().ShouldBe(id);
        Links(again.GetProperty("guardian")).ShouldBe(2);

        // E2 / phạm vi chi nhánh / quan hệ "Bản thân"
        await Expect(await staff.PostAsJsonAsync(Base, Body(Phone()), Ct), HttpStatusCode.BadRequest, "crm.guardian.students_required");
        await Expect(await staff.PostAsJsonAsync(Base, Body(Phone(), "ST-3"), Ct), HttpStatusCode.Forbidden, "crm.branch_not_allowed");

        // Sửa liên hệ + kênh nhận (chống sửa đè)
        var version = again.GetProperty("guardian").GetProperty("version").GetUInt32();
        var edited = await Ok(await staff.PutAsJsonAsync($"{Base}/{id}",
            new { fullName = "Ngô Thị Lan Anh", phone, email = "lan.anh@gmail.com", branchId = Cg, notifyChannels = new[] { "sms", "email" }, version }, Ct));
        edited.GetProperty("guardian").GetProperty("fullName").GetString().ShouldBe("Ngô Thị Lan Anh");
        edited.GetProperty("guardian").GetProperty("notifyChannels").GetArrayLength().ShouldBe(2);
        await Expect(await staff.PutAsJsonAsync($"{Base}/{id}",
            new { fullName = "X", phone, branchId = Cg, version }, Ct), HttpStatusCode.Conflict);

        // Cờ liên kết + gỡ liên kết (luật CRM-08)
        var st9 = SeedData.IdOf("ST-9");
        var flags = await Ok(await staff.PutAsJsonAsync($"{Base}/{id}/students/{st9}",
            new { relationship = "sibling", primary = false, billing = true, notify = false }, Ct));
        flags.GetProperty("students").EnumerateArray().Single(k => k.GetProperty("studentId").GetGuid() == st9)
            .GetProperty("relationship").GetString().ShouldBe("sibling");
        (await Ok(await staff.DeleteAsync($"{Base}/{id}/students/{st9}", Ct))).GetProperty("students").GetArrayLength().ShouldBe(1);
        await Expect(await staff.PostAsJsonAsync($"{Base}/{id}/students",
            new { studentId = SeedData.IdOf("ST-3"), relationship = "other", primary = false, billing = false, notify = true }, Ct),
            HttpStatusCode.Forbidden, "crm.branch_not_allowed");
        await Ok(await staff.PostAsJsonAsync($"{Base}/{id}/students",
            new { studentId = st9, relationship = "other", primary = false, billing = false, notify = true }, Ct));

        // Admin (Tuyển sinh) thấy người liên hệ này như 1 lead đã chốt
        var admin = await SignIn(Admin);
        (await Ok(await admin.GetAsync($"/api/v1/crm/leads/{id}", Ct))).GetProperty("lead").GetProperty("status").GetString().ShouldBe("won");
    }

    [Fact(DisplayName = "HV-12: không có quyền students.contacts → 403")]
    public async Task Guardians_require_contacts_permission()
    {
        var sale = await SignIn(Sale);
        await Expect(await sale.GetAsync(Base, Ct), HttpStatusCode.Forbidden);
        await Expect(await sale.PostAsJsonAsync(Base, Body(Phone(), "ST-5"), Ct), HttpStatusCode.Forbidden);
    }
}
