using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text.Json;
using JPCenter.Infrastructure.Common.Persistence;
using JPCenter.IntegrationTests.Infrastructure;

namespace JPCenter.IntegrationTests.Academic;

/// <summary>
/// User story 07 (đợt 1) — phòng, lớp, buổi học trên API + PostgreSQL thật với dữ liệu mẫu.
/// Lớp tạo mới dùng ngày năm 2027 + mỗi test 1 khung giờ Chủ nhật riêng để không trùng lịch với nhau / với dữ liệu mẫu.
/// </summary>
[Collection(SeededCollection.Name)]
public sealed class ClassFlowTests(SeededApiFactory api)
{
    private const string Classes = "/api/v1/academic/classes";
    private const string Sessions = "/api/v1/academic/class-sessions";
    private const string Rooms = "/api/v1/academic/rooms";
    private static CancellationToken Ct => TestContext.Current.CancellationToken;
    private static int _seq;

    private static readonly Guid Cg = SeedData.IdOf("cg");
    private static readonly Guid N5Course = SeedData.IdOf("CRS-01");
    private static readonly Guid N5Curriculum = SeedData.IdOf("CUR-N5-V2");
    private static readonly Guid RoomP201 = SeedData.IdOf("R-CG-201");
    private static readonly Guid RoomP301 = SeedData.IdOf("R-CG-301");
    private static readonly Guid RoomP302 = SeedData.IdOf("R-CG-302");
    private static readonly Guid RoomP303Maintenance = SeedData.IdOf("R-CG-303");
    private static readonly Guid TeacherAnh = SeedData.IdOf("t-anh");
    private static readonly Guid TeacherLong = SeedData.IdOf("t-long");
    private static readonly Guid TeacherHa = SeedData.IdOf("t-ha");
    private static readonly Guid ClassN3K12 = SeedData.IdOf("C-N3K12");
    private static readonly Guid ClassN5K20Q1 = SeedData.IdOf("C-N5K20");

    /// <summary>Chủ nhật đầu tiên của lớp thử</summary>
    private static readonly DateOnly Start = new(2027, 3, 7);

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

    private static async Task<string> Code(HttpResponseMessage r)
    {
        var body = await Json(r);
        return body.TryGetProperty("code", out var c) ? c.GetString()! : body.GetProperty("errors").EnumerateObject().First().Value[0].GetString()!;
    }

    /// <summary>API bỏ trường null khỏi JSON</summary>
    private static bool Absent(JsonElement e, string name) => !e.TryGetProperty(name, out var v) || v.ValueKind == JsonValueKind.Null;

    private static string Suffix() => $"T{Interlocked.Increment(ref _seq)}{Random.Shared.Next(100, 999)}";

    private static object NewClass(string code, Guid teacher, Guid room, string start, string end, decimal totalHours = 20, int max = 15,
        bool generate = true, uint? rowVersion = null, DateOnly? startDate = null) => new
    {
        courseId = N5Course, curriculumId = N5Curriculum, code, name = "Lớp thử " + code, branchId = Cg, teacherIds = new[] { teacher },
        assistantIds = Array.Empty<Guid>(), roomId = room, online = false, meetingUrl = (string?)null, plannedStartDate = startDate ?? Start,
        plannedEndDate = (DateOnly?)null, weekdays = new[] { 7 }, startTime = start, endTime = end, totalHours, minStudents = 5, maxStudents = max,
        fee = 4_900_000m, previousClassId = (Guid?)null, note = (string?)null, generateSessions = generate, applyToUpcoming = true, rowVersion,
    };

    private static async Task<JsonElement> SessionsOf(HttpClient c, Guid classId) => await Ok(await c.GetAsync($"{Classes}/{classId}/sessions", Ct));

    // ---------------- Phòng học ----------------

    [Fact(DisplayName = "Phòng: thêm, trùng tên, bảo trì, ngừng khi còn lớp dùng, báo hỏng → nhận sửa → xong → mở lại")]
    public async Task Rooms_lifecycle()
    {
        var c = await SignIn();
        var name = "P.T" + Random.Shared.Next(1000, 9999);
        var room = await Ok(await c.PostAsJsonAsync(Rooms, new
        {
            branchId = Cg, name, floor = "Tầng 9", type = "classroom", capacity = 18, area = 30m, equipment = new[] { "projector", "aircon" },
            note = (string?)null, rowVersion = (uint?)null,
        }, Ct));
        var id = room.GetProperty("id").GetGuid();
        room.GetProperty("status").GetString().ShouldBe("active");

        var dup = await c.PostAsJsonAsync(Rooms, new { branchId = Cg, name = " " + name.ToLowerInvariant(), type = "classroom", capacity = 10 }, Ct);
        dup.StatusCode.ShouldBe(HttpStatusCode.Conflict);
        (await Code(dup)).ShouldBe("academic.room.duplicate_name");

        var maint = await Ok(await c.PostAsJsonAsync($"{Rooms}/{id}/status", new { status = "maintenance", reason = "Sơn lại", until = "2027-01-10" }, Ct));
        maint.GetProperty("status").GetString().ShouldBe("maintenance");
        (await Ok(await c.PostAsJsonAsync($"{Rooms}/{id}/status", new { status = "active" }, Ct))).GetProperty("status").GetString().ShouldBe("active");

        // P.301 đang có lớp GVV_N3-K12 học → không ngừng được
        var inUse = await c.PostAsJsonAsync($"{Rooms}/{RoomP301}/status", new { status = "inactive" }, Ct);
        inUse.StatusCode.ShouldBe(HttpStatusCode.Conflict);
        (await Code(inUse)).ShouldBe("academic.room.in_use");
        var p301 = (await Ok(await c.GetAsync($"{Rooms}?branchId={Cg}", Ct))).EnumerateArray().Single(r => r.GetProperty("id").GetGuid() == RoomP301);
        p301.GetProperty("openClasses").EnumerateArray().Select(x => x.GetString()).ShouldContain("GVV_N3-K12");

        var issue = await Ok(await c.PostAsJsonAsync($"{Rooms}/issues", new { roomId = id, equipment = "projector", title = "Máy chiếu mờ", severity = "medium" }, Ct));
        var issueId = issue.GetProperty("id").GetGuid();
        issue.GetProperty("status").GetString().ShouldBe("open");
        (await Ok(await c.PostAsJsonAsync($"{Rooms}/issues/{issueId}/start", new { assignee = "Kỹ thuật tòa nhà", dueDate = "2027-01-05" }, Ct)))
            .GetProperty("status").GetString().ShouldBe("in_progress");
        var resolved = await Ok(await c.PostAsJsonAsync($"{Rooms}/issues/{issueId}/resolve", new { resolution = "Thay bóng", cost = 1_200_000m }, Ct));
        resolved.GetProperty("status").GetString().ShouldBe("resolved");
        resolved.GetProperty("cost").GetDecimal().ShouldBe(1_200_000m);
        var reopened = await Ok(await c.PostAsync($"{Rooms}/issues/{issueId}/reopen", null, Ct));
        reopened.GetProperty("status").GetString().ShouldBe("in_progress");
        Absent(reopened, "resolution").ShouldBeTrue();
    }

    [Fact(DisplayName = "Phòng: lễ tân chỉ xem — thêm phòng 403")]
    public async Task Rooms_receptionist_cannot_create()
    {
        var c = await SignIn("hang.hang@jpcenter.vn");
        (await c.GetAsync(Rooms, Ct)).StatusCode.ShouldBe(HttpStatusCode.OK);
        var r = await c.PostAsJsonAsync(Rooms, new { branchId = Cg, name = "P.X", type = "classroom", capacity = 10 }, Ct);
        r.StatusCode.ShouldBe(HttpStatusCode.Forbidden);
    }

    // ---------------- Lớp học ----------------

    [Fact(DisplayName = "DT-02 + DT-08: mở lớp sinh lịch theo lộ trình, bỏ ngày lễ; DT-02.E8 trùng GV / phòng bị chặn")]
    public async Task Open_class_generates_schedule_and_blocks_conflicts()
    {
        var c = await SignIn("giaovu@jpcenter.vn");
        var code = "GVV_N5-" + Suffix();
        var created = await c.PostAsJsonAsync(Classes, NewClass(code, TeacherAnh, RoomP201, "13:00", "15:00"), Ct);
        created.StatusCode.ShouldBe(HttpStatusCode.Created, await created.Content.ReadAsStringAsync(Ct));
        var cls = (await Json(created)).GetProperty("class");
        var id = cls.GetProperty("id").GetGuid();
        cls.GetProperty("status").GetString().ShouldBe("enrolling");
        cls.GetProperty("code").GetString().ShouldBe(code);
        cls.GetProperty("room").GetString().ShouldBe("P.201");
        cls.GetProperty("startTime").GetString().ShouldBe("13:00");
        cls.GetProperty("sessionCount").GetInt32().ShouldBe(10);
        cls.GetProperty("fee").GetDecimal().ShouldBe(4_900_000m);

        var sessions = (await SessionsOf(c, id)).EnumerateArray().ToList();
        sessions.Count.ShouldBe(10);
        sessions.Select(s => s.GetProperty("no").GetInt32()).ShouldBe(Enumerable.Range(1, 10));
        sessions.ShouldAllBe(s => DateOnly.Parse(s.GetProperty("date").GetString()!).DayOfWeek == DayOfWeek.Sunday);
        sessions.ShouldAllBe(s => s.GetProperty("status").GetString() == "scheduled");
        sessions.ShouldContain(s => !Absent(s, "lessonId"));
        var last = sessions[^1].GetProperty("date").GetString();
        (await Ok(await c.GetAsync($"{Classes}/{id}", Ct))).GetProperty("plannedEndDate").GetString().ShouldBe(last);

        // Cùng GV + cùng phòng + cùng giờ → chặn; /check liệt kê lớp trùng
        var clash = NewClass("GVV_N5-" + Suffix(), TeacherAnh, RoomP201, "14:00", "16:00");
        var check = await Ok(await c.PostAsJsonAsync($"{Classes}/check", clash, Ct));
        check.GetProperty("conflicts").EnumerateArray().Select(x => x.GetProperty("kind").GetString()).ShouldBe(["teacher", "room"], ignoreOrder: true);
        var blocked = await c.PostAsJsonAsync(Classes, clash, Ct);
        blocked.StatusCode.ShouldBe(HttpStatusCode.Conflict);
        (await Code(blocked)).ShouldBe("academic.class.schedule_conflict");

        // Mã trùng, phòng bảo trì, sai tiền tố
        (await Code(await c.PostAsJsonAsync(Classes, NewClass(code.ToLowerInvariant(), TeacherAnh, RoomP201, "06:00", "08:00"), Ct)))
            .ShouldBe("academic.class.duplicate_code");
        (await Code(await c.PostAsJsonAsync(Classes, NewClass("GVV_N5-" + Suffix(), TeacherAnh, RoomP303Maintenance, "06:00", "08:00"), Ct)))
            .ShouldBe("academic.room.unavailable");
        (await Code(await c.PostAsJsonAsync(Classes, NewClass("GVN_N5-" + Suffix(), TeacherAnh, RoomP201, "06:00", "08:00"), Ct)))
            .ShouldBe("academic.class.invalid_code");
    }

    [Fact(DisplayName = "DT-03 → DT-06: khai giảng dời lịch, tạm dừng, học lại, kết thúc hủy buổi chưa dạy")]
    public async Task Class_lifecycle()
    {
        var c = await SignIn();
        var code = "GVV_N5-" + Suffix();
        var cls = (await Ok(await c.PostAsJsonAsync(Classes, NewClass(code, TeacherLong, RoomP302, "15:30", "17:30"), Ct))).GetProperty("class");
        var id = cls.GetProperty("id").GetGuid();

        var started = await Ok(await c.PostAsJsonAsync($"{Classes}/{id}/start", new { actualStartDate = "2027-03-14", reschedule = true }, Ct));
        started.GetProperty("class").GetProperty("status").GetString().ShouldBe("ongoing");
        started.GetProperty("class").GetProperty("actualStartDate").GetString().ShouldBe("2027-03-14");
        var sessions = (await SessionsOf(c, id)).EnumerateArray().ToList();
        sessions.Count.ShouldBe(10);
        sessions[0].GetProperty("date").GetString().ShouldBe("2027-03-14");

        (await Code(await c.PostAsJsonAsync($"{Classes}/{id}/pause", new { reason = " " }, Ct))).ShouldBe("academic.class.pause_reason");
        (await Ok(await c.PostAsJsonAsync($"{Classes}/{id}/pause", new { reason = "GV ốm dài ngày" }, Ct)))
            .GetProperty("class").GetProperty("status").GetString().ShouldBe("paused");
        var resumed = await Ok(await c.PostAsync($"{Classes}/{id}/resume", null, Ct));
        resumed.GetProperty("class").GetProperty("status").GetString().ShouldBe("ongoing");
        resumed.GetProperty("warnings").GetArrayLength().ShouldBe(1);

        var finished = await Ok(await c.PostAsJsonAsync($"{Classes}/{id}/finish", new { actualEndDate = "2027-03-20" }, Ct));
        finished.GetProperty("class").GetProperty("status").GetString().ShouldBe("finished");
        finished.GetProperty("warnings")[0].GetString()!.ShouldContain("Đã hủy 10 buổi");
        (await SessionsOf(c, id)).EnumerateArray().ShouldAllBe(s => s.GetProperty("status").GetString() == "cancelled");

        (await Code(await c.PostAsJsonAsync($"{Classes}/{id}/pause", new { reason = "x" }, Ct))).ShouldBe("academic.class.not_ongoing");
    }

    [Fact(DisplayName = "DT-07: sửa lớp — đổi giờ áp cho buổi chưa dạy; sửa đè 409")]
    public async Task Update_class_applies_new_time()
    {
        var c = await SignIn();
        var code = "GVV_N5-" + Suffix();
        var cls = (await Ok(await c.PostAsJsonAsync(Classes, NewClass(code, TeacherHa, RoomP301, "19:00", "21:00",
            startDate: new DateOnly(2027, 6, 6)), Ct))).GetProperty("class");
        var id = cls.GetProperty("id").GetGuid();
        var version = cls.GetProperty("rowVersion").GetUInt32();

        var updated = await Ok(await c.PutAsJsonAsync($"{Classes}/{id}",
            NewClass(code, TeacherHa, RoomP301, "19:30", "21:30", rowVersion: version, startDate: new DateOnly(2027, 6, 6)), Ct));
        updated.GetProperty("class").GetProperty("startTime").GetString().ShouldBe("19:30");
        (await SessionsOf(c, id)).EnumerateArray().ShouldAllBe(s => s.GetProperty("startTime").GetString() == "19:30");

        var stale = await c.PutAsJsonAsync($"{Classes}/{id}",
            NewClass(code, TeacherHa, RoomP301, "19:00", "21:00", rowVersion: version, startDate: new DateOnly(2027, 6, 6)), Ct);
        stale.StatusCode.ShouldBe(HttpStatusCode.Conflict);
    }

    [Fact(DisplayName = "DT-10, DT-11, DT-05: dời buổi, hủy buổi, học bù 1 lần, hủy lớp phải gõ đúng mã")]
    public async Task Session_changes_and_cancel_class()
    {
        var c = await SignIn("giaovu@jpcenter.vn");
        var code = "GVV_N5-" + Suffix();
        var cls = (await Ok(await c.PostAsJsonAsync(Classes, NewClass(code, TeacherAnh, RoomP201, "06:00", "08:00", totalHours: 8,
            startDate: new DateOnly(2027, 8, 1)), Ct))).GetProperty("class");
        var id = cls.GetProperty("id").GetGuid();
        var sessions = (await SessionsOf(c, id)).EnumerateArray().ToList();
        sessions.Count.ShouldBe(4);

        // Dời buổi 1 sang thứ Bảy
        var s1 = sessions[0];
        var moved = await Ok(await c.PutAsJsonAsync($"{Sessions}/{s1.GetProperty("id").GetGuid()}", new
        {
            date = "2027-07-31", startTime = "06:00", endTime = "08:00", teacherIds = new[] { TeacherAnh }, roomId = RoomP201,
            reason = "Trùng lịch thi", rowVersion = s1.GetProperty("rowVersion").GetUInt32(),
        }, Ct));
        var m = moved.GetProperty("session");
        m.GetProperty("date").GetString().ShouldBe("2027-07-31");
        m.GetProperty("rescheduledFrom").GetProperty("date").GetString().ShouldBe("2027-08-01");
        Absent(m, "teacherIds").ShouldBeTrue(); // giống lớp = không ghi đè

        // Hủy buổi 2 → học bù 1 lần
        var s2 = sessions[1].GetProperty("id").GetGuid();
        (await Code(await c.PostAsJsonAsync($"{Sessions}/{s2}/cancel", new { reason = "" }, Ct))).ShouldBe("validation.failed");
        var cancelled = await Ok(await c.PostAsJsonAsync($"{Sessions}/{s2}/cancel", new { reason = "Mất điện" }, Ct));
        cancelled.GetProperty("session").GetProperty("status").GetString().ShouldBe("cancelled");
        cancelled.GetProperty("session").GetProperty("cancelledBy").GetProperty("name").GetString().ShouldNotBe("—");

        var makeup = new { classId = id, origin = "makeup", makeupForId = s2, date = "2027-09-11", startTime = "06:00", endTime = "08:00" };
        var made = await Ok(await c.PostAsJsonAsync(Sessions, makeup, Ct));
        made.GetProperty("session").GetProperty("origin").GetString().ShouldBe("makeup");
        made.GetProperty("session").GetProperty("no").GetInt32().ShouldBe(2);
        (await Code(await c.PostAsJsonAsync(Sessions, makeup, Ct))).ShouldBe("academic.session.already_made_up");

        // Học thêm: ngoài số buổi, "Tạo lịch tự động" không xóa
        (await Ok(await c.PostAsJsonAsync(Sessions, new { classId = id, origin = "extra", date = "2027-09-12", startTime = "06:00", endTime = "08:00" }, Ct)))
            .GetProperty("session").GetProperty("no").GetInt32().ShouldBe(0);
        var preview = await Ok(await c.PostAsJsonAsync($"{Classes}/{id}/sessions/preview", new { from = "2027-10-03" }, Ct));
        preview.GetProperty("kept").GetInt32().ShouldBe(3); // buổi hủy + học bù + học thêm
        preview.GetProperty("replaced").GetInt32().ShouldBe(3);
        preview.GetProperty("overridesLost").GetInt32().ShouldBe(1); // buổi đã dời
        preview.GetProperty("count").GetInt32().ShouldBe(3); // 4 buổi khóa − 1 buổi học bù đã xếp (buổi hủy không tính)

        (await Code(await c.PostAsJsonAsync($"{Classes}/{id}/cancel", new { reason = "Không đủ HV", confirmCode = "SAI" }, Ct)))
            .ShouldBe("academic.class.cancel_confirm");
        var gone = await Ok(await c.PostAsJsonAsync($"{Classes}/{id}/cancel", new { reason = "Không đủ HV", confirmCode = code.ToLowerInvariant() }, Ct));
        gone.GetProperty("class").GetProperty("status").GetString().ShouldBe("cancelled");
        (await SessionsOf(c, id)).EnumerateArray().ShouldAllBe(s => s.GetProperty("status").GetString() == "cancelled");
    }

    // ---------------- Xác nhận buổi & quyền ----------------

    [Fact(DisplayName = "DT-15: bỏ xác nhận → xác nhận lại; buổi chưa tới giờ không xác nhận được; xác nhận hàng loạt trả lý do lỗi")]
    public async Task Confirm_and_unconfirm()
    {
        var c = await SignIn();
        var sessions = (await SessionsOf(c, ClassN3K12)).EnumerateArray().ToList();
        var confirmed = sessions.First(s => !Absent(s, "confirmation"));
        var id = confirmed.GetProperty("id").GetGuid();

        var un = await Ok(await c.PostAsync($"{Sessions}/{id}/unconfirm", null, Ct));
        Absent(un.GetProperty("session"), "confirmation").ShouldBeTrue();
        un.GetProperty("session").GetProperty("status").GetString().ShouldBe("scheduled");

        var again = await Ok(await c.PostAsJsonAsync($"{Sessions}/{id}/confirm", new { note = "Đã kiểm tra" }, Ct));
        again.GetProperty("session").GetProperty("status").GetString().ShouldBe("completed");
        again.GetProperty("session").GetProperty("confirmation").GetProperty("note").GetString().ShouldBe("Đã kiểm tra");
        (await Code(await c.PostAsync($"{Sessions}/{id}/confirm", null, Ct))).ShouldBe("academic.session.already_confirmed");

        var future = sessions.Last(s => s.GetProperty("status").GetString() == "scheduled"
            && DateOnly.Parse(s.GetProperty("date").GetString()!) > DateOnly.FromDateTime(DateTime.Today.AddDays(1)));
        var bulk = await Ok(await c.PostAsJsonAsync($"{Sessions}/confirm", new { ids = new[] { future.GetProperty("id").GetGuid(), Guid.NewGuid() } }, Ct));
        bulk.GetProperty("confirmed").GetInt32().ShouldBe(0);
        bulk.GetProperty("failed").EnumerateArray().Select(f => f.GetProperty("code").GetString())
            .ShouldBe(["academic.session.not_finished", "academic.session.not_found"], ignoreOrder: true);
    }

    [Fact(DisplayName = "Quyền: lễ tân xem buổi trong chi nhánh, không xem lớp / xác nhận; giáo vụ CG không thấy lớp Q1")]
    public async Task Permissions_and_branch_scope()
    {
        var lt = await SignIn("hang.hang@jpcenter.vn");
        (await lt.GetAsync(Classes, Ct)).StatusCode.ShouldBe(HttpStatusCode.Forbidden);
        var today = DateOnly.FromDateTime(DateTime.Today);
        var day = await Ok(await lt.GetAsync($"{Sessions}?from={today.AddDays(-30):yyyy-MM-dd}&to={today.AddDays(30):yyyy-MM-dd}", Ct));
        day.GetArrayLength().ShouldBeGreaterThan(0);
        day.EnumerateArray().ShouldAllBe(s => s.GetProperty("branchId").GetGuid() == Cg);
        var any = (await SessionsOf(await SignIn(), ClassN3K12)).EnumerateArray().First().GetProperty("id").GetGuid();
        (await lt.PostAsync($"{Sessions}/{any}/confirm", null, Ct)).StatusCode.ShouldBe(HttpStatusCode.Forbidden);
        (await lt.PostAsJsonAsync(Classes, NewClass("GVV_N5-" + Suffix(), TeacherAnh, RoomP201, "05:00", "06:00"), Ct))
            .StatusCode.ShouldBe(HttpStatusCode.Forbidden);

        var gv = await SignIn("giaovu@jpcenter.vn");
        (await gv.GetAsync($"{Classes}/{ClassN5K20Q1}", Ct)).StatusCode.ShouldBe(HttpStatusCode.NotFound);
        (await Ok(await gv.GetAsync(Classes, Ct))).EnumerateArray().ShouldNotContain(x => x.GetProperty("id").GetGuid() == ClassN5K20Q1);

        // Kế toán không có quyền lớp
        (await (await SignIn("ketoan@jpcenter.vn")).GetAsync(Classes, Ct)).StatusCode.ShouldBe(HttpStatusCode.Forbidden);
    }
}
