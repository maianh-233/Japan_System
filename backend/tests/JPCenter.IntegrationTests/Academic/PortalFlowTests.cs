using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text.Json;
using JPCenter.Application.Common.Messaging;
using JPCenter.Infrastructure.Common.Persistence;
using JPCenter.IntegrationTests.Infrastructure;
using JPCenter.Modules.Academic.Application.Requests.ExpireInvites;
using Microsoft.Extensions.DependencyInjection;

namespace JPCenter.IntegrationTests.Academic;

/// <summary>
/// Giai đoạn 15 phase 3 — cổng GV (điểm danh, chấm công, báo cáo, đơn hủy buổi, dạy thay, nhận xét), cổng HV / PH (quét QR, xin nghỉ, học bù,
/// Quan tâm lớp kỳ sau), phía giáo vụ (duyệt đơn, mời dạy thay, lên lớp) và cảnh báo bỏ học — API + PostgreSQL thật với dữ liệu mẫu.
/// Mỗi test mở lớp riêng vào thứ Bảy, khung giờ sáng sớm riêng để không trùng lịch GV / phòng.
/// </summary>
[Collection(SeededCollection.Name)]
public sealed class PortalFlowTests(SeededApiFactory api)
{
    private const string Academic = "/api/v1/academic";
    private const string Teaching = "/api/v1/academic/portal/teaching";
    private const string Learning = "/api/v1/academic/portal/learning";
    private static CancellationToken Ct => TestContext.Current.CancellationToken;
    private static int _seq;

    private static readonly Guid Cg = SeedData.IdOf("cg");
    private static readonly Guid N5Course = SeedData.IdOf("CRS-01");
    private static readonly Guid N5Curriculum = SeedData.IdOf("CUR-N5-V2");
    private static readonly Guid RoomP201 = SeedData.IdOf("R-CG-201");
    private static readonly Guid RoomP301 = SeedData.IdOf("R-CG-301");
    private static readonly Guid RoomP302 = SeedData.IdOf("R-CG-302");
    private static readonly Guid TeacherHa = SeedData.IdOf("t-ha");
    private static readonly Guid TeacherLong = SeedData.IdOf("t-long");
    private static readonly Guid TeacherAnh = SeedData.IdOf("t-anh");
    private static readonly Guid St1 = SeedData.IdOf("ST-1");

    /// <summary>Giáo vụ (mở lớp, điểm danh hộ) · quản lý chi nhánh (có <c>academic.requests.approve</c> — duyệt đơn, mời dạy thay)</summary>
    private const string Staff = "giaovu@jpcenter.vn", Manager = "quanly.caugiay@jpcenter.vn", Ha = "giaovien@jpcenter.vn", Long = "long.t.long@jpcenter.vn", Anh = "anh.t.anh@jpcenter.vn";
    private const string StudentAcc = "hocvien@jpcenter.vn", ParentAcc = "phuhuynh@jpcenter.vn";

    /// <summary>Thứ Bảy gần nhất cách hôm nay ≥ 3 ngày (trong cửa sổ học bù 3 tuần)</summary>
    private static DateOnly NearSaturday()
    {
        var d = DateOnly.FromDateTime(DateTime.UtcNow.AddHours(7)).AddDays(3);
        while (d.DayOfWeek != DayOfWeek.Saturday) d = d.AddDays(1);
        return d;
    }

    private static readonly DateOnly FarSaturday = new(2027, 3, 6);

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

    private static async Task<string> Code(HttpResponseMessage r)
    {
        var body = await Json(r);
        return body.TryGetProperty("code", out var c) && c.GetString() != "validation.failed"
            ? c.GetString()!
            : body.GetProperty("errors").EnumerateObject().First().Value[0].GetString()!;
    }

    private static async Task Expect(HttpResponseMessage r, HttpStatusCode status, string code)
    {
        r.StatusCode.ShouldBe(status, await r.Content.ReadAsStringAsync(Ct));
        (await Code(r)).ShouldBe(code);
    }

    private static bool Absent(JsonElement e, string name) => !e.TryGetProperty(name, out var v) || v.ValueKind == JsonValueKind.Null;

    private static string Suffix() => $"P{Interlocked.Increment(ref _seq)}{Random.Shared.Next(100, 999)}";

    /// <summary>Mở lớp N5 thứ Bảy → (Id lớp, danh sách Id buổi theo số thứ tự)</summary>
    private static async Task<(Guid Id, List<Guid> Sessions)> OpenClass(HttpClient c, Guid teacher, Guid room, string start, string end, DateOnly startDate)
    {
        var code = "GVV_N5-" + Suffix();
        var r = await c.PostAsJsonAsync($"{Academic}/classes", new
        {
            courseId = N5Course, curriculumId = N5Curriculum, code, name = "Lớp cổng " + code, branchId = Cg, teacherIds = new[] { teacher },
            assistantIds = Array.Empty<Guid>(), roomId = room, online = false, meetingUrl = (string?)null, plannedStartDate = startDate,
            plannedEndDate = (DateOnly?)null, weekdays = new[] { 6 }, startTime = start, endTime = end, totalHours = 10m, minStudents = 1, maxStudents = 15,
            fee = 4_900_000m, previousClassId = (Guid?)null, note = (string?)null, generateSessions = true, applyToUpcoming = true, rowVersion = (uint?)null,
        }, Ct);
        r.StatusCode.ShouldBe(HttpStatusCode.Created, await r.Content.ReadAsStringAsync(Ct));
        var id = (await Json(r)).GetProperty("class").GetProperty("id").GetGuid();
        var sessions = (await Ok(await c.GetAsync($"{Academic}/classes/{id}/sessions", Ct))).EnumerateArray()
            .OrderBy(s => s.GetProperty("no").GetInt32()).Select(s => s.GetProperty("id").GetGuid()).ToList();
        return (id, sessions);
    }

    private static async Task Enroll(HttpClient c, Guid studentId, Guid classId) =>
        await Ok(await c.PostAsJsonAsync("/api/v1/students/enrollments", new { studentId, classId }, Ct));

    private static async Task<Guid> NewStudent(HttpClient c)
    {
        var r = await Ok(await c.PostAsJsonAsync("/api/v1/students/import",
            new { rows = new[] { new { fullName = "HV cổng " + Suffix(), branchId = Cg } } }, Ct));
        return r.GetProperty("created")[0].GetProperty("id").GetGuid();
    }

    private static async Task<JsonElement> Session(HttpClient staff, Guid id) => await Ok(await staff.GetAsync($"{Academic}/class-sessions/{id}", Ct));

    private static JsonElement RecordOf(JsonElement session, Guid student) =>
        session.GetProperty("records").EnumerateArray().Single(r => r.GetProperty("studentId").GetGuid() == student);

    private static async Task Eventually(Func<Task<bool>> condition)
    {
        for (var i = 0; i < 40; i++)
        {
            if (await condition()) return;
            await Task.Delay(250, Ct);
        }
        (await condition()).ShouldBeTrue("Hết thời gian chờ việc chạy nền");
    }

    // ---------------- Cổng GV ----------------

    [Fact(DisplayName = "Cổng GV: lịch dạy, điểm danh, QR, check-in sớm bị chặn, báo cáo buổi; GV khác / sai cổng bị chặn; giáo vụ điểm danh & chấm công hộ")]
    public async Task Teacher_portal_attendance_and_report()
    {
        var staff = await SignIn(Staff);
        var start = NearSaturday();
        var (classId, sessions) = await OpenClass(staff, TeacherHa, RoomP201, "05:00", "06:00", start);
        var other = await NewStudent(staff);
        await Enroll(staff, St1, classId);
        await Enroll(staff, other, classId);
        var s1 = sessions[0];

        var ha = await SignIn(Ha);
        var mine = await Ok(await ha.GetAsync($"{Teaching}/sessions?from={start:yyyy-MM-dd}&to={start.AddDays(60):yyyy-MM-dd}", Ct));
        mine.EnumerateArray().Select(s => s.GetProperty("id").GetGuid()).ShouldContain(s1);

        var att = await Ok(await ha.PutAsJsonAsync($"{Teaching}/sessions/{s1}/attendance", new
        {
            records = new object[] { new { studentId = St1, status = "present", checkInAt = "05:02" }, new { studentId = other, status = "absent" } },
        }, Ct));
        var saved = att.GetProperty("session");
        RecordOf(saved, St1).GetProperty("status").GetString().ShouldBe("present");
        RecordOf(saved, St1).GetProperty("method").GetString().ShouldBe("teacher");
        saved.GetProperty("attendance").GetProperty("present").GetInt32().ShouldBe(1);

        await Expect(await ha.PutAsJsonAsync($"{Teaching}/sessions/{s1}/attendance",
            new { records = new[] { new { studentId = Guid.NewGuid(), status = "present" } } }, Ct), HttpStatusCode.Forbidden, "academic.attendance.not_in_roster");

        var qr = await Ok(await ha.GetAsync($"{Teaching}/sessions/{s1}/qr", Ct));
        qr.GetProperty("payload").GetString()!.ShouldStartWith($"JPC-ATT|{s1}|");
        qr.GetProperty("code").GetString()!.Length.ShouldBe(6);
        qr.GetProperty("isOpen").GetBoolean().ShouldBeFalse();

        await Expect(await ha.PostAsync($"{Teaching}/sessions/{s1}/check-in", null, Ct), HttpStatusCode.Conflict, "academic.teacher_attendance.too_early");
        await Expect(await ha.PostAsync($"{Teaching}/sessions/{s1}/check-out", null, Ct), HttpStatusCode.Conflict, "academic.teacher_attendance.not_checked_in");

        var report = await Ok(await ha.PutAsJsonAsync($"{Teaching}/sessions/{s1}/report",
            new { content = "Bài 1: chào hỏi", homework = "Viết 10 câu", coverage = "full", coveredChapterIds = Array.Empty<Guid>() }, Ct));
        report.GetProperty("session").GetProperty("teacherLog").GetProperty("content").GetString().ShouldBe("Bài 1: chào hỏi");
        report.GetProperty("warnings").EnumerateArray().ShouldNotBeEmpty(); // chưa check-in

        // GV không dạy buổi này / tài khoản nhân viên gọi cổng GV / GV gọi trang giáo vụ
        var longClient = await SignIn(Long);
        (await longClient.GetAsync($"{Teaching}/sessions/{s1}", Ct)).StatusCode.ShouldBe(HttpStatusCode.NotFound);
        await Expect(await longClient.PutAsJsonAsync($"{Teaching}/sessions/{s1}/attendance",
            new { records = new[] { new { studentId = St1, status = "late" } } }, Ct), HttpStatusCode.Forbidden, "academic.teacher_attendance.not_assigned");
        await Expect(await staff.GetAsync($"{Teaching}/cancel-requests", Ct), HttpStatusCode.Forbidden, "system.permission.wrong_portal");
        (await ha.GetAsync($"{Academic}/requests/cancel", Ct)).StatusCode.ShouldBe(HttpStatusCode.Forbidden);

        // Giáo vụ điểm danh hộ (nhật ký hỗ trợ) + chấm công hộ GV vắng chưa có người thay → cảnh báo
        var byStaff = await Ok(await staff.PutAsJsonAsync($"{Academic}/class-sessions/{sessions[1]}/attendance",
            new { records = new[] { new { studentId = St1, status = "late", checkInAt = "05:15" } } }, Ct));
        RecordOf(byStaff.GetProperty("session"), St1).GetProperty("method").GetString().ShouldBe("staff");
        byStaff.GetProperty("session").GetProperty("supportLog").EnumerateArray().ShouldContain(l => l.GetProperty("kind").GetString() == "attendance");
        var ta = await Ok(await staff.PutAsJsonAsync($"{Academic}/class-sessions/{sessions[1]}/teacher-attendance",
            new { teacherId = TeacherHa, role = "teacher", status = "absent" }, Ct));
        ta.GetProperty("warnings").EnumerateArray().ShouldNotBeEmpty();
        ta.GetProperty("session").GetProperty("teacherAttendance")[0].GetProperty("source").GetString().ShouldBe("staff");
        (await Ok(await staff.GetAsync($"{Academic}/class-sessions/{s1}/qr", Ct))).GetProperty("code").GetString()
            .ShouldBe(qr.GetProperty("code").GetString());
    }

    [Fact(DisplayName = "DT-16: GV xin hủy buổi → giáo vụ mời dạy thay → GV nhận trước dạy, lời mời khác hết hiệu lực; trùng đơn, thu hồi, từ chối, hết hạn")]
    public async Task Cancel_request_and_substitute()
    {
        var staff = await SignIn(Staff);
        var (_, sessions) = await OpenClass(staff, TeacherHa, RoomP201, "05:00", "06:00", FarSaturday);
        var ha = await SignIn(Ha);
        var manager = await SignIn(Manager);

        await Expect(await ha.PostAsJsonAsync($"{Teaching}/cancel-requests",
            new { sessionId = sessions[2], reasonType = "Ốm", reason = "Ốm" }, Ct), HttpStatusCode.BadRequest, "academic.cancel_request.reason_required");
        var request = await Ok(await ha.PostAsJsonAsync($"{Teaching}/cancel-requests",
            new { sessionId = sessions[2], reasonType = "Ốm / sức khỏe", reason = "Sốt cao, có giấy khám bệnh", makeupAt = "2027-03-30T11:00:00Z" }, Ct));
        request.GetProperty("status").GetString().ShouldBe("pending");
        var requestId = request.GetProperty("id").GetGuid();
        await Expect(await ha.PostAsJsonAsync($"{Teaching}/cancel-requests",
            new { sessionId = sessions[2], reasonType = "Ốm", reason = "Sốt cao, có giấy khám bệnh" }, Ct), HttpStatusCode.Conflict,
            "academic.cancel_request.duplicate");
        (await Ok(await ha.GetAsync($"{Teaching}/cancel-requests", Ct))).EnumerateArray().Select(x => x.GetProperty("id").GetGuid()).ShouldContain(requestId);
        (await Ok(await staff.GetAsync($"{Academic}/requests/cancel?status=pending", Ct))).EnumerateArray()
            .Select(x => x.GetProperty("id").GetGuid()).ShouldContain(requestId);

        var candidates = (await Ok(await staff.GetAsync($"{Academic}/substitute-invites/candidates?sessionId={sessions[2]}", Ct))).EnumerateArray()
            .Select(x => x.GetProperty("teacherId").GetGuid()).ToList();
        candidates.ShouldContain(TeacherLong);
        candidates.ShouldNotContain(TeacherHa);

        // Giáo vụ không có quyền duyệt (academic.requests.approve) trong dữ liệu mẫu
        (await staff.PostAsJsonAsync($"{Academic}/requests/cancel/{requestId}/approve", new { }, Ct)).StatusCode.ShouldBe(HttpStatusCode.Forbidden);
        var invites = await Ok(await manager.PostAsJsonAsync($"{Academic}/substitute-invites", new
        {
            sessionId = sessions[2], cancelRequestId = requestId, teacherIds = new[] { TeacherLong, TeacherAnh }, respondBy = DateTimeOffset.UtcNow.AddDays(2),
            reason = "GV Hà ốm",
        }, Ct));
        invites.GetArrayLength().ShouldBe(2);
        var approved = (await Ok(await staff.GetAsync($"{Academic}/requests/cancel", Ct))).EnumerateArray().Single(x => x.GetProperty("id").GetGuid() == requestId);
        approved.GetProperty("status").GetString().ShouldBe("approved");
        approved.GetProperty("note").GetString()!.ShouldStartWith("Đã mời GV");

        var longClient = await SignIn(Long);
        var myInvite = (await Ok(await longClient.GetAsync($"{Teaching}/substitute-invites", Ct))).EnumerateArray()
            .Single(x => x.GetProperty("sessionId").GetGuid() == sessions[2]);
        var accepted = await Ok(await longClient.PostAsync($"{Teaching}/substitute-invites/{myInvite.GetProperty("id").GetGuid()}/accept", null, Ct));
        accepted.GetProperty("status").GetString().ShouldBe("accepted");
        (await Session(staff, sessions[2])).GetProperty("teacherIds").EnumerateArray().Select(x => x.GetGuid()).ShouldBe([TeacherLong]);

        var all = (await Ok(await staff.GetAsync($"{Academic}/substitute-invites?sessionId={sessions[2]}", Ct))).EnumerateArray().ToList();
        var anhInvite = all.Single(x => x.GetProperty("toTeacherId").GetGuid() == TeacherAnh);
        (anhInvite.GetProperty("status").GetString(), anhInvite.GetProperty("declineReason").GetString()).ShouldBe(("expired", "GV khác đã nhận"));
        var anh = await SignIn(Anh);
        await Expect(await anh.PostAsync($"{Teaching}/substitute-invites/{anhInvite.GetProperty("id").GetGuid()}/accept", null, Ct),
            HttpStatusCode.Conflict, "academic.invite.closed");
        // GV mới (dạy thay) thấy buổi trong lịch dạy
        (await Ok(await longClient.GetAsync($"{Teaching}/sessions/{sessions[2]}", Ct))).GetProperty("id").GetGuid().ShouldBe(sessions[2]);

        // Thu hồi đơn còn chờ; từ chối cần lý do
        var w = await Ok(await ha.PostAsJsonAsync($"{Teaching}/cancel-requests",
            new { sessionId = sessions[3], reasonType = "Việc gia đình", reason = "Việc gia đình đột xuất" }, Ct));
        (await ha.DeleteAsync($"{Teaching}/cancel-requests/{w.GetProperty("id").GetGuid()}", Ct)).StatusCode.ShouldBe(HttpStatusCode.NoContent);
        (await Ok(await ha.GetAsync($"{Teaching}/cancel-requests", Ct))).EnumerateArray()
            .ShouldNotContain(x => x.GetProperty("id").GetGuid() == w.GetProperty("id").GetGuid());
        var rj = (await Ok(await ha.PostAsJsonAsync($"{Teaching}/cancel-requests",
            new { sessionId = sessions[4], reasonType = "Khác", reason = "Tham dự hội thảo giáo viên" }, Ct))).GetProperty("id").GetGuid();
        (await manager.PostAsJsonAsync($"{Academic}/requests/cancel/{rj}/reject", new { note = "" }, Ct)).StatusCode.ShouldBe(HttpStatusCode.BadRequest);
        (await Ok(await manager.PostAsJsonAsync($"{Academic}/requests/cancel/{rj}/reject", new { note = "Lớp sắp thi" }, Ct)))
            .GetProperty("status").GetString().ShouldBe("rejected");
        await Expect(await manager.PostAsJsonAsync($"{Academic}/requests/cancel/{rj}/approve", new { note = "x" }, Ct), HttpStatusCode.Conflict,
            "academic.request.not_pending");

        // Job hết hạn lời mời (DT-16 bước 3)
        var shortInvite = (await Ok(await manager.PostAsJsonAsync($"{Academic}/substitute-invites", new
        {
            sessionId = sessions[5], teacherIds = new[] { TeacherAnh }, respondBy = DateTimeOffset.UtcNow.AddSeconds(2), reason = "Thử hết hạn",
        }, Ct)))[0].GetProperty("id").GetGuid();
        await Task.Delay(2500, Ct);
        using (var scope = api.Services.CreateScope())
            (await scope.ServiceProvider.GetRequiredService<ISender>().Send(new ExpireInvitesCommand(), Ct)).Value.ShouldBeGreaterThanOrEqualTo(1);
        var expired = (await Ok(await staff.GetAsync($"{Academic}/substitute-invites?sessionId={sessions[5]}", Ct))).EnumerateArray()
            .Single(x => x.GetProperty("id").GetGuid() == shortInvite);
        (expired.GetProperty("status").GetString(), expired.GetProperty("declineReason").GetString()).ShouldBe(("expired", "Quá hạn trả lời"));
    }

    // ---------------- Cổng HV / PH ----------------

    [Fact(DisplayName = "DT-18: HV xin nghỉ + chọn học bù lớp khác → giáo vụ duyệt → vắng có phép, có tên ở buổi bù, kết quả bù ghi về đơn; QR; PH chưa có con")]
    public async Task Leave_request_with_makeup()
    {
        var staff = await SignIn(Staff);
        var start = NearSaturday();
        var (classC, sc) = await OpenClass(staff, TeacherHa, RoomP201, "06:10", "07:10", start);
        var (_, sd) = await OpenClass(staff, TeacherLong, RoomP302, "07:20", "08:20", start);
        await Enroll(staff, St1, classC);

        var student = await SignIn(StudentAcc);
        (await Ok(await student.GetAsync($"{Learning}/sessions?from={start:yyyy-MM-dd}&to={start.AddDays(7):yyyy-MM-dd}", Ct))).EnumerateArray()
            .Select(s => s.GetProperty("id").GetGuid()).ShouldContain(sc[0]);
        var options = (await Ok(await student.GetAsync($"{Learning}/leave-requests/makeup-options?sessionId={sc[0]}", Ct))).EnumerateArray()
            .Select(o => o.GetProperty("sessionId").GetGuid()).ToList();
        options.ShouldContain(sd[0]);

        await Expect(await student.PostAsJsonAsync($"{Learning}/leave-requests",
            new { sessionId = sc[0], reasonType = "Ốm", reason = "Ốm" }, Ct), HttpStatusCode.BadRequest, "academic.leave_request.reason_required");
        await Expect(await student.PostAsJsonAsync($"{Learning}/leave-requests",
            new { sessionId = sc[0], reasonType = "Ốm", reason = "Bị sốt cần nghỉ", makeupSessionId = sc[1] }, Ct), HttpStatusCode.BadRequest,
            "academic.leave_request.makeup_invalid");
        var leave = await Ok(await student.PostAsJsonAsync($"{Learning}/leave-requests",
            new { sessionId = sc[0], reasonType = "Ốm / sức khỏe", reason = "Bị sốt cần nghỉ", makeupSessionId = sd[0] }, Ct));
        (leave.GetProperty("status").GetString(), leave.GetProperty("by").GetString()).ShouldBe(("pending", "student"));
        leave.GetProperty("makeup").GetProperty("sessionId").GetGuid().ShouldBe(sd[0]);
        var leaveId = leave.GetProperty("id").GetGuid();
        await Expect(await student.PostAsJsonAsync($"{Learning}/leave-requests",
            new { sessionId = sc[0], reasonType = "Ốm", reason = "Bị sốt cần nghỉ" }, Ct), HttpStatusCode.Conflict, "academic.leave_request.duplicate");

        (await Ok(await staff.GetAsync($"{Academic}/requests/leave?status=pending&classId={classC}", Ct))).EnumerateArray()
            .Select(x => x.GetProperty("id").GetGuid()).ShouldContain(leaveId);
        var manager = await SignIn(Manager);
        var decided = await Ok(await manager.PostAsJsonAsync($"{Academic}/requests/leave/{leaveId}/approve", new { note = "Đã duyệt" }, Ct));
        decided.GetProperty("request").GetProperty("status").GetString().ShouldBe("approved");
        RecordOf(await Session(staff, sc[0]), St1).GetProperty("status").GetString().ShouldBe("excused");
        RecordOf(await Session(staff, sd[0]), St1).GetProperty("makeupFor").GetProperty("sessionId").GetGuid().ShouldBe(sc[0]);

        // GV lớp bù điểm danh khách học bù → kết quả về đơn + buổi gốc tính đã học bù
        var longClient = await SignIn(Long);
        await Ok(await longClient.PutAsJsonAsync($"{Teaching}/sessions/{sd[0]}/attendance",
            new { records = new[] { new { studentId = St1, status = "present" } } }, Ct));
        (await Ok(await student.GetAsync($"{Learning}/leave-requests", Ct))).EnumerateArray().Single(x => x.GetProperty("id").GetGuid() == leaveId)
            .GetProperty("makeupResult").GetString().ShouldBe("present");
        Absent(RecordOf(await Session(staff, sc[0]), St1), "madeUp").ShouldBeFalse();

        // Thu hồi đơn còn chờ
        var w = (await Ok(await student.PostAsJsonAsync($"{Learning}/leave-requests",
            new { sessionId = sc[2], reasonType = "Việc gia đình", reason = "Về quê có việc" }, Ct))).GetProperty("id").GetGuid();
        (await student.DeleteAsync($"{Learning}/leave-requests/{w}", Ct)).StatusCode.ShouldBe(HttpStatusCode.NoContent);

        // Quét QR: sai mã / đúng mã nhưng chưa tới giờ
        await Expect(await student.PostAsJsonAsync($"{Learning}/attendance/scan", new { payload = $"JPC-ATT|{sc[1]}|SAIMA1" }, Ct),
            HttpStatusCode.BadRequest, "academic.attendance.invalid_code");
        var qr = await Ok(await staff.GetAsync($"{Academic}/class-sessions/{sc[1]}/qr", Ct));
        await Expect(await student.PostAsJsonAsync($"{Learning}/attendance/scan", new { payload = qr.GetProperty("payload").GetString() }, Ct),
            HttpStatusCode.Conflict, "academic.attendance.qr_closed");

        // PH (Crm chưa có liên kết con — giai đoạn 16): xem rỗng, gửi đơn 403; PH không quét QR; GV không vào cổng HV
        var parent = await SignIn(ParentAcc);
        (await Ok(await parent.GetAsync($"{Learning}/leave-requests", Ct))).GetArrayLength().ShouldBe(0);
        await Expect(await parent.PostAsJsonAsync($"{Learning}/leave-requests",
            new { studentId = St1, sessionId = sc[3], reasonType = "Ốm", reason = "Con bị ốm" }, Ct), HttpStatusCode.Forbidden, "academic.portal.not_your_child");
        await Expect(await parent.PostAsJsonAsync($"{Learning}/attendance/scan", new { code = "ABCDEF" }, Ct), HttpStatusCode.Forbidden,
            "system.permission.wrong_portal");
        (await longClient.GetAsync($"{Learning}/leave-requests", Ct)).StatusCode.ShouldBe(HttpStatusCode.Forbidden);
    }

    [Fact(DisplayName = "DT-17 + DT-19: GV nhận xét cuối khóa; giáo vụ đưa HV vào danh sách lên lớp, giữ chỗ lớp kỳ sau; HV bấm Quan tâm; giao Sale")]
    public async Task Promotion_and_evaluation()
    {
        var staff = await SignIn(Staff);
        var (classE, _) = await OpenClass(staff, TeacherHa, RoomP201, "06:10", "07:10", FarSaturday);
        var (classF, _) = await OpenClass(staff, TeacherLong, RoomP302, "07:20", "08:20", FarSaturday.AddDays(84));
        await Enroll(staff, St1, classE);

        var ha = await SignIn(Ha);
        var body = new
        {
            skills = new { vocab = 4, grammar = 4, reading = 3, listening = 5, speaking = 4 }, attitude = 5, strengths = "Chăm chỉ",
            recommendation = "next_level", advice = "Nên học tối",
        };
        var eval = await Ok(await ha.PutAsJsonAsync($"{Teaching}/classes/{classE}/evaluations/{St1}", body, Ct));
        (eval.GetProperty("recommendation").GetString(), eval.GetProperty("skills").GetProperty("listening").GetInt32()).ShouldBe(("next_level", 5));
        await Expect(await ha.PutAsJsonAsync($"{Teaching}/classes/{classE}/evaluations/{St1}", body with { attitude = 6 }, Ct),
            HttpStatusCode.BadRequest, "academic.evaluation.invalid_rating");
        await Expect(await (await SignIn(Long)).PutAsJsonAsync($"{Teaching}/classes/{classE}/evaluations/{St1}", body, Ct),
            HttpStatusCode.Forbidden, "academic.teacher_attendance.not_assigned");
        (await Ok(await staff.GetAsync($"{Academic}/classes/{classE}/evaluations", Ct))).EnumerateArray().Single()
            .GetProperty("author").GetString().ShouldBe("Nguyễn Thu Hà");

        await Expect(await staff.PostAsJsonAsync($"{Academic}/classes/{classE}/promotions", new { studentIds = new[] { Guid.NewGuid() } }, Ct),
            HttpStatusCode.BadRequest, "academic.promotion.not_in_class");
        var list = await Ok(await staff.PostAsJsonAsync($"{Academic}/classes/{classE}/promotions", new { studentIds = new[] { St1 }, source = "teacher" }, Ct));
        var promotionId = list.EnumerateArray().Single().GetProperty("id").GetGuid();
        await Expect(await staff.PutAsJsonAsync($"{Academic}/promotions/{promotionId}", new { holdSeat = true }, Ct), HttpStatusCode.BadRequest,
            "academic.promotion.to_class_required");
        var held = await Ok(await staff.PutAsJsonAsync($"{Academic}/promotions/{promotionId}", new { toClassId = classF, holdSeat = true }, Ct));
        (held.GetProperty("promotion").GetProperty("seatHeld").GetBoolean(), Absent(held.GetProperty("promotion"), "holdUntil")).ShouldBe((true, false));

        var student = await SignIn(StudentAcc);
        var offer = (await Ok(await student.GetAsync($"{Learning}/promotions", Ct))).EnumerateArray().Single(o => o.GetProperty("id").GetGuid() == promotionId);
        offer.GetProperty("toClassId").GetGuid().ShouldBe(classF);
        (await Ok(await student.PostAsync($"{Learning}/promotions/{promotionId}/interest", null, Ct))).GetProperty("interest").GetProperty("by").GetString()
            .ShouldBe("student");
        // Nhận xét cuối khóa không có ở cổng HV
        (await student.GetAsync($"{Teaching}/classes/{classE}/evaluations", Ct)).StatusCode.ShouldBe(HttpStatusCode.Forbidden);

        var handed = await Ok(await staff.PostAsJsonAsync($"{Academic}/classes/{classE}/promotions/hand-over", new { }, Ct));
        var p = handed.EnumerateArray().Single();
        Absent(p, "handedOverAt").ShouldBeFalse();
        p.GetProperty("interest").GetProperty("by").GetString().ShouldBe("student");
    }

    // ---------------- Cảnh báo bỏ học (Students) ----------------

    [Fact(DisplayName = "HV-14: vắng 3 buổi liên tiếp → cảnh báo khi điểm danh + case nguy cơ cao; giao người, ghi nhận liên hệ; GV không xem được")]
    public async Task At_risk_from_attendance()
    {
        var staff = await SignIn(Staff);
        var (classG, sessions) = await OpenClass(staff, TeacherAnh, RoomP301, "04:00", "05:00", FarSaturday);
        var student = await NewStudent(staff);
        await Enroll(staff, student, classG);

        JsonElement last = default;
        foreach (var s in sessions.Take(3))
            last = await Ok(await staff.PutAsJsonAsync($"{Academic}/class-sessions/{s}/attendance",
                new { records = new[] { new { studentId = student, status = "absent" } } }, Ct));
        last.GetProperty("warnings").EnumerateArray().Select(w => w.GetString()).ShouldContain(w => w!.Contains("vắng 2 buổi liên tiếp"));

        JsonElement found = default;
        await Eventually(async () =>
        {
            var cases = (await Ok(await staff.GetAsync($"/api/v1/students/at-risk?classId={classG}", Ct))).EnumerateArray()
                .Where(x => x.GetProperty("studentId").GetGuid() == student).ToList();
            if (cases.Count == 0 || cases[0].GetProperty("riskLevel").GetString() != "high") return false;
            found = cases[0];
            return true;
        });
        found.GetProperty("flags").EnumerateArray().Select(f => f.GetString()).ShouldContain("streak");
        found.GetProperty("stats").GetProperty("streak").GetInt32().ShouldBe(3);
        var caseId = found.GetProperty("id").GetGuid();

        var updated = await Ok(await staff.PutAsJsonAsync($"/api/v1/students/at-risk/{caseId}",
            new { assigneeId = SeedData.IdOf("e-mai"), status = "monitoring", nextAt = "2027-03-20" }, Ct));
        (updated.GetProperty("status").GetString(), updated.GetProperty("nextAt").GetString()).ShouldBe(("monitoring", "2027-03-20"));
        (await staff.PostAsJsonAsync($"/api/v1/students/at-risk/{caseId}/logs", new { channel = "call", note = " " }, Ct))
            .StatusCode.ShouldBe(HttpStatusCode.BadRequest);
        await Ok(await staff.PostAsJsonAsync($"/api/v1/students/at-risk/{caseId}/logs", new { channel = "zalo", note = "Đã nhắn PH" }, Ct));
        (await Ok(await staff.GetAsync($"/api/v1/students/care-logs?classId={classG}", Ct))).GetArrayLength().ShouldBe(1);
        (await Ok(await staff.PostAsync("/api/v1/students/at-risk/recalculate", null, Ct))).GetInt32().ShouldBeGreaterThanOrEqualTo(1);

        var teacher = await SignIn(Ha);
        (await teacher.GetAsync("/api/v1/students/at-risk", Ct)).StatusCode.ShouldBe(HttpStatusCode.Forbidden);
        (await teacher.GetAsync($"/api/v1/students/care-logs?classId={classG}", Ct)).StatusCode.ShouldBe(HttpStatusCode.Forbidden);
    }

    // ---------------- Realtime ----------------

    [Fact(DisplayName = "SignalR: hub /hubs/realtime cần đăng nhập (token header hoặc query access_token)")]
    public async Task Realtime_hub_requires_sign_in()
    {
        var anonymous = api.CreateClient();
        (await anonymous.PostAsync("/hubs/realtime/negotiate?negotiateVersion=1", null, Ct)).StatusCode.ShouldBe(HttpStatusCode.Unauthorized);

        var teacher = await SignIn(Ha);
        (await Ok(await teacher.PostAsync("/hubs/realtime/negotiate?negotiateVersion=1", null, Ct))).GetProperty("connectionToken").GetString()
            .ShouldNotBeNullOrEmpty();

        var token = teacher.DefaultRequestHeaders.Authorization!.Parameter;
        var viaQuery = api.CreateClient();
        (await viaQuery.PostAsync($"/hubs/realtime/negotiate?negotiateVersion=1&access_token={token}", null, Ct)).StatusCode.ShouldBe(HttpStatusCode.OK);
        // Token qua query chỉ nhận ở đường dẫn hub
        (await viaQuery.GetAsync($"{Teaching}/cancel-requests?access_token={token}", Ct)).StatusCode.ShouldBe(HttpStatusCode.Unauthorized);
    }
}
