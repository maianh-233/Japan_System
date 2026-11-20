using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text.Json;
using JPCenter.Infrastructure.Common.Persistence;
using JPCenter.IntegrationTests.Infrastructure;

namespace JPCenter.IntegrationTests.Students;

/// <summary>User story 06 (giai đoạn 14) — hồ sơ học viên, nhập Excel, ẩn, ghi danh, bảo lưu, chuyển lớp, cho nghỉ; nối với Lớp học.</summary>
[Collection(SeededCollection.Name)]
public sealed class StudentFlowTests(SeededApiFactory api)
{
    private const string Students = "/api/v1/students";
    private const string Enrollments = "/api/v1/students/enrollments";
    private const string Classes = "/api/v1/academic/classes";
    private static CancellationToken Ct => TestContext.Current.CancellationToken;
    private static int _seq;

    private static readonly Guid Cg = SeedData.IdOf("cg");
    private static readonly Guid Q1 = SeedData.IdOf("q1");
    private static readonly Guid An = SeedData.IdOf("ST-1");
    private static readonly Guid Phuc = SeedData.IdOf("ST-3");
    private static readonly Guid ClassN5K21 = SeedData.IdOf("C-N5K21");
    private static readonly Guid ClassN4K17 = SeedData.IdOf("C-N4K17");
    private static readonly Guid ClassN5K18Finished = SeedData.IdOf("C-N5K18");

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
        return JsonDocument.Parse(body).RootElement;
    }

    private static async Task<string> Code(HttpResponseMessage r) => (await Json(r)).GetProperty("code").GetString()!;

    private static string Name() => $"Học viên thử {Interlocked.Increment(ref _seq)}{Random.Shared.Next(100, 999)}";

    private static async Task<JsonElement> Import(HttpClient c, string? phone = null, Guid? branch = null)
    {
        var r = await Ok(await c.PostAsJsonAsync($"{Students}/import", new { rows = new[] { new { fullName = Name(), phone, branchId = branch ?? Cg } } }, Ct));
        return r.GetProperty("created")[0];
    }

    private static async Task<JsonElement> Enroll(HttpClient c, Guid studentId, Guid classId) =>
        (await Ok(await c.PostAsJsonAsync(Enrollments, new { studentId, classId, discount = 0, dueDate = "2026-12-31" }, Ct))).GetProperty("enrollments")[0];

    private static async Task Eventually(Func<Task<bool>> condition)
    {
        for (var i = 0; i < 40; i++)
        {
            if (await condition()) return;
            await Task.Delay(250, Ct);
        }
        (await condition()).ShouldBeTrue("Hết thời gian chờ việc chạy nền");
    }

    // ---------------- Hồ sơ ----------------

    [Fact(DisplayName = "HV-01, HV-02: danh sách theo phạm vi, trạng thái học suy từ ghi danh, HV ẩn chỉ hiện khi yêu cầu")]
    public async Task List_and_scope()
    {
        var admin = await SignIn();
        var all = (await Ok(await admin.GetAsync($"{Students}?includeHidden=true", Ct))).EnumerateArray().ToList();
        var visible = (await Ok(await admin.GetAsync(Students, Ct))).EnumerateArray().ToList();
        all.Count.ShouldBeGreaterThan(visible.Count);
        visible.ShouldAllBe(s => !s.GetProperty("hidden").GetBoolean());

        var an = all.Single(s => s.GetProperty("id").GetGuid() == An);
        an.GetProperty("studyStatus").GetString().ShouldBe("studying");
        an.GetProperty("activeClassIds").EnumerateArray().Select(x => x.GetGuid()).ShouldContain(SeedData.IdOf("C-N3K12"));

        // Giáo vụ (Cầu Giấy + Hải Châu) không thấy HV Quận 1
        var gv = await SignIn("giaovu@jpcenter.vn");
        (await Ok(await gv.GetAsync(Students, Ct))).EnumerateArray().ShouldNotContain(s => s.GetProperty("branchId").GetGuid() == Q1);
        (await gv.GetAsync($"{Students}/{Phuc}", Ct)).StatusCode.ShouldBe(HttpStatusCode.NotFound);

        // Ghi danh của HV: tab Lớp học trong hồ sơ
        var history = (await Ok(await admin.GetAsync($"{Enrollments}?studentId={An}", Ct))).EnumerateArray().ToList();
        // ST-1 là tài khoản cổng HV duy nhất của dữ liệu mẫu → test cổng (PortalFlowTests) ghi danh thêm lớp mới; chỉ xét ghi danh mẫu
        history.Where(e => e.GetProperty("id").GetGuid().ToString()[14] == '3').Select(e => e.GetProperty("status").GetString())
            .ShouldBe(["completed", "studying"], ignoreOrder: true);
        history.Single(e => e.GetProperty("status").GetString() == "completed").GetProperty("certificate").GetProperty("sentBy").GetString()
            .ShouldBe("Phạm Thị Hồng");
    }

    [Fact(DisplayName = "HV-03: sửa hồ sơ — SĐT sai, ngày sinh tương lai bị chặn; sửa đè 409; Sale chỉ xem")]
    public async Task Update_profile()
    {
        var c = await SignIn("giaovu@jpcenter.vn");
        var s = await Import(c);
        var id = s.GetProperty("id").GetGuid();
        var version = s.GetProperty("rowVersion").GetUInt32();
        object Body(string? phone = "0987 654 321", string dob = "2005-03-02", uint? v = null) =>
            new { fullName = "Trần  Thị   Mai", gender = "female", dob, phone, email = "Mai@Mail.vn", address = "Hà Nội", note = "Ghi chú", branchId = Cg, rowVersion = v ?? version };

        (await Code(await c.PutAsJsonAsync($"{Students}/{id}", Body(phone: "123"), Ct))).ShouldBe("students.student.invalid_phone");
        (await Code(await c.PutAsJsonAsync($"{Students}/{id}", Body(dob: "2030-01-01"), Ct))).ShouldBe("students.student.invalid_dob");
        var ok = await Ok(await c.PutAsJsonAsync($"{Students}/{id}", Body(), Ct));
        (ok.GetProperty("fullName").GetString(), ok.GetProperty("phone").GetString(), ok.GetProperty("email").GetString())
            .ShouldBe(("Trần Thị Mai", "0987654321", "mai@mail.vn"));
        (await c.PutAsJsonAsync($"{Students}/{id}", Body(), Ct)).StatusCode.ShouldBe(HttpStatusCode.Conflict);

        var sale = await SignIn("tung.tung@jpcenter.vn");
        (await sale.GetAsync($"{Students}/{id}", Ct)).StatusCode.ShouldBe(HttpStatusCode.OK);
        (await sale.PutAsJsonAsync($"{Students}/{id}", Body(v: ok.GetProperty("rowVersion").GetUInt32()), Ct)).StatusCode.ShouldBe(HttpStatusCode.Forbidden);
    }

    [Fact(DisplayName = "HV-04: nhập Excel — dòng lỗi bỏ qua, dòng trùng SĐT + tên chờ xác nhận, mã HV tăng dần")]
    public async Task Import_rows()
    {
        var c = await SignIn();
        var name = Name();
        var r = await Ok(await c.PostAsJsonAsync($"{Students}/import", new
        {
            rows = new object[]
            {
                new { fullName = name, phone = "0911222333", branchId = Cg },
                new { fullName = " ", branchId = Cg },
                new { fullName = "Nguyễn Văn An", phone = "0912000111", branchId = Cg },
            },
        }, Ct));
        r.GetProperty("created").GetArrayLength().ShouldBe(1);
        r.GetProperty("created")[0].GetProperty("studyStatus").GetString().ShouldBe("none");
        r.GetProperty("created")[0].GetProperty("code").GetString()!.ShouldStartWith("HV");
        r.GetProperty("errors")[0].GetProperty("row").GetInt32().ShouldBe(2);
        var dup = r.GetProperty("duplicates")[0];
        (dup.GetProperty("row").GetInt32(), dup.GetProperty("existingCode").GetString()).ShouldBe((3, "HV1000"));

        var again = await Ok(await c.PostAsJsonAsync($"{Students}/import", new
        {
            rows = new[] { new { fullName = "Nguyễn Văn An", phone = "0912000111", branchId = Cg } },
            importDuplicates = true,
        }, Ct));
        again.GetProperty("created").GetArrayLength().ShouldBe(1);
        int Num(JsonElement e) => int.Parse(e.GetProperty("code").GetString()![2..]);
        Num(again.GetProperty("created")[0]).ShouldBeGreaterThan(Num(r.GetProperty("created")[0]));
    }

    [Fact(DisplayName = "HV-05: ẩn hàng loạt — HV còn học bị bỏ qua kèm lớp; bỏ ẩn")]
    public async Task Hide_students()
    {
        var c = await SignIn();
        var fresh = (await Import(c)).GetProperty("id").GetGuid();
        var r = await Ok(await c.PostAsJsonAsync($"{Students}/hide", new { ids = new[] { An, fresh } }, Ct));
        r.GetProperty("hidden").EnumerateArray().Select(x => x.GetProperty("id").GetGuid()).ShouldBe([fresh]);
        var skipped = r.GetProperty("skipped")[0];
        skipped.GetProperty("code").GetString().ShouldBe("HV1000");
        skipped.GetProperty("reason").GetString()!.ShouldContain("GVV_N3-K12");

        (await Ok(await c.PostAsync($"{Students}/{fresh}/unhide", null, Ct))).GetProperty("hidden").GetBoolean().ShouldBeFalse();
    }

    // ---------------- Ghi danh ----------------

    [Fact(DisplayName = "HV-06 → HV-10: ghi danh, trùng, bảo lưu, học lại, chuyển lớp, cho nghỉ; sĩ số lớp đổi theo")]
    public async Task Enrollment_lifecycle()
    {
        var c = await SignIn("giaovu@jpcenter.vn");
        var student = (await Import(c)).GetProperty("id").GetGuid();
        int Enrolled(JsonElement cls) => cls.GetProperty("enrolled").GetInt32();
        var before = Enrolled(await Ok(await c.GetAsync($"{Classes}/{ClassN5K21}", Ct)));

        var e = await Enroll(c, student, ClassN5K21);
        var id = e.GetProperty("id").GetGuid();
        (e.GetProperty("status").GetString(), e.GetProperty("classCode").GetString()).ShouldBe(("studying", "GVV_N5-K21"));
        Enrolled(await Ok(await c.GetAsync($"{Classes}/{ClassN5K21}", Ct))).ShouldBe(before + 1);
        (await Code(await c.PostAsJsonAsync(Enrollments, new { studentId = student, classId = ClassN5K21 }, Ct))).ShouldBe("students.enrollment.duplicate");
        (await Code(await c.PostAsJsonAsync(Enrollments, new { studentId = student, classId = ClassN5K18Finished }, Ct))).ShouldBe("academic.class.closed");

        // Bảo lưu → học lại
        var today = DateOnly.FromDateTime(DateTime.Today);
        (await Code(await c.PostAsJsonAsync($"{Enrollments}/{id}/reserve", new { until = today.AddMonths(7), reason = "Ốm" }, Ct)))
            .ShouldBe("students.enrollment.invalid_reserve_until");
        var reserved = (await Ok(await c.PostAsJsonAsync($"{Enrollments}/{id}/reserve", new { until = today.AddMonths(1), reason = "Đi công tác" }, Ct)))
            .GetProperty("enrollments")[0];
        reserved.GetProperty("status").GetString().ShouldBe("reserved");
        reserved.GetProperty("note").GetString()!.ShouldEndWith("— Đi công tác");
        (await Ok(await c.PostAsync($"{Enrollments}/{id}/resume", null, Ct))).GetProperty("enrollments")[0].GetProperty("status").GetString()
            .ShouldBe("studying");

        // Chuyển lớp (khấu trừ dư → cảnh báo hoàn tay)
        (await Code(await c.PostAsJsonAsync($"{Enrollments}/transfer", new
        {
            targetClassId = ClassN5K21, items = new[] { new { enrollmentId = id, amount = 1m, credit = 0m } }, reason = "x",
        }, Ct))).ShouldBe("students.enrollment.same_class");
        var moved = await Ok(await c.PostAsJsonAsync($"{Enrollments}/transfer", new
        {
            targetClassId = ClassN4K17, items = new[] { new { enrollmentId = id, amount = 3_000_000m, credit = 3_500_000m } }, dueDate = "2026-12-31",
            reason = "Đổi lịch học",
        }, Ct));
        var list = moved.GetProperty("enrollments").EnumerateArray().ToList();
        var old = list.Single(x => x.GetProperty("id").GetGuid() == id);
        var next = list.Single(x => x.GetProperty("id").GetGuid() != id);
        (old.GetProperty("status").GetString(), old.GetProperty("transferredTo").GetGuid()).ShouldBe(("transferred", next.GetProperty("id").GetGuid()));
        (next.GetProperty("status").GetString(), next.GetProperty("transferredFrom").GetGuid()).ShouldBe(("studying", id));
        next.GetProperty("note").GetString().ShouldBe("Chuyển từ lớp GVV_N5-K21 — Đổi lịch học");
        moved.GetProperty("warnings")[0].GetString()!.ShouldContain("hoàn tay");
        Enrolled(await Ok(await c.GetAsync($"{Classes}/{ClassN5K21}", Ct))).ShouldBe(before);

        // Cho nghỉ: lần nghỉ cuối → gợi ý ẩn
        var nextId = next.GetProperty("id").GetGuid();
        (await Code(await c.PostAsJsonAsync($"{Enrollments}/{nextId}/drop", new { reason = "" }, Ct))).ShouldBe("students.enrollment.reason_required");
        var dropped = await Ok(await c.PostAsJsonAsync($"{Enrollments}/{nextId}/drop", new { reason = "Chuyển nhà" }, Ct));
        dropped.GetProperty("enrollments")[0].GetProperty("status").GetString().ShouldBe("dropped");
        dropped.GetProperty("warnings").GetArrayLength().ShouldBe(1);
        (await Ok(await c.GetAsync($"{Students}/{student}", Ct))).GetProperty("studyStatus").GetString().ShouldBe("dropped");
    }

    [Fact(DisplayName = "HV-06.E1 lớp đầy; HV-10.E2 lớp bị hủy → ghi danh tự thành Đã nghỉ")]
    public async Task Full_class_and_class_cancelled()
    {
        var c = await SignIn();
        var code = $"GVV_N5-HV{Interlocked.Increment(ref _seq)}{Random.Shared.Next(100, 999)}";
        var cls = (await Ok(await c.PostAsJsonAsync(Classes, new
        {
            courseId = SeedData.IdOf("CRS-01"), curriculumId = SeedData.IdOf("CUR-N5-V2"), code, name = "Lớp 1 chỗ", branchId = Cg,
            teacherIds = new[] { SeedData.IdOf("t-tanaka") }, roomId = SeedData.IdOf("R-CG-401"), online = false, plannedStartDate = "2027-11-07",
            weekdays = new[] { 7 }, startTime = "20:00", endTime = "21:00", totalHours = 2m, minStudents = 1, maxStudents = 1, fee = 1_000_000m,
            generateSessions = false,
        }, Ct))).GetProperty("class");
        var classId = cls.GetProperty("id").GetGuid();

        var first = (await Enroll(c, (await Import(c)).GetProperty("id").GetGuid(), classId)).GetProperty("id").GetGuid();
        var second = await c.PostAsJsonAsync(Enrollments, new { studentId = (await Import(c)).GetProperty("id").GetGuid(), classId }, Ct);
        (await Code(second)).ShouldBe("academic.class.full");

        await Ok(await c.PostAsJsonAsync($"{Classes}/{classId}/cancel", new { reason = "Không đủ HV", confirmCode = code }, Ct));
        await Eventually(async () =>
        {
            var e = (await Ok(await c.GetAsync($"{Enrollments}?classId={classId}", Ct))).EnumerateArray().Single(x => x.GetProperty("id").GetGuid() == first);
            return e.GetProperty("status").GetString() == "dropped" && e.GetProperty("note").GetString() == "Lớp bị hủy: Không đủ HV";
        });
    }

    [Fact(DisplayName = "Quyền: kế toán xem hồ sơ nhưng không ghi danh; GV không xem học viên")]
    public async Task Permissions()
    {
        var kt = await SignIn("ketoan@jpcenter.vn");
        (await kt.GetAsync(Students, Ct)).StatusCode.ShouldBe(HttpStatusCode.OK);
        (await kt.PostAsJsonAsync(Enrollments, new { studentId = An, classId = ClassN5K21 }, Ct)).StatusCode.ShouldBe(HttpStatusCode.Forbidden);
        (await kt.PostAsJsonAsync($"{Students}/hide", new { ids = new[] { An } }, Ct)).StatusCode.ShouldBe(HttpStatusCode.Forbidden);

        var gv = await SignIn("giaovien@jpcenter.vn");
        (await gv.GetAsync(Students, Ct)).StatusCode.ShouldBe(HttpStatusCode.Forbidden);
    }
}
