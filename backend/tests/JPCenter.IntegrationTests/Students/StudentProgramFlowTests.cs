using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text.Json;
using JPCenter.Infrastructure.Common.Persistence;
using JPCenter.IntegrationTests.Infrastructure;

namespace JPCenter.IntegrationTests.Students;

/// <summary>User story 06 HV-15 → 17 (giai đoạn 23 phase 3) — chứng nhận hoàn thành, thi JLPT, hồ sơ du học / XKLĐ.</summary>
[Collection(SeededCollection.Name)]
public sealed class StudentProgramFlowTests(SeededApiFactory api)
{
    private const string Students = "/api/v1/students";
    private const string Portal = "/api/v1/students/portal/learning";
    private const string Admin = "admin@jpcenter.vn", GiaoVu = "giaovu@jpcenter.vn", Sale = "tung.tung@jpcenter.vn",
        StudentAcc = "hocvien@jpcenter.vn", ParentAcc = "phuhuynh@jpcenter.vn";
    private static CancellationToken Ct => TestContext.Current.CancellationToken;
    private static int _seq;

    private static readonly Guid Cg = SeedData.IdOf("cg");
    private static readonly Guid ClassN5K21 = SeedData.IdOf("C-N5K21");
    private static readonly Guid EmployeeHa = SeedData.IdOf("t-ha");
    private static readonly Guid St1 = SeedData.IdOf("ST-1");

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
        return JsonDocument.Parse(body).RootElement;
    }

    private static async Task Expect(HttpResponseMessage r, HttpStatusCode status, string? code = null)
    {
        var body = await r.Content.ReadAsStringAsync(Ct);
        r.StatusCode.ShouldBe(status, body);
        if (code is not null) JsonDocument.Parse(body).RootElement.GetProperty("code").GetString().ShouldBe(code);
    }

    private static async Task<Guid> NewStudent(HttpClient c)
    {
        var name = $"HV chương trình {Interlocked.Increment(ref _seq)}{Random.Shared.Next(100, 999)}";
        var r = await Ok(await c.PostAsJsonAsync($"{Students}/import", new { rows = new[] { new { fullName = name, branchId = Cg } } }, Ct));
        return r.GetProperty("created")[0].GetProperty("id").GetGuid();
    }

    // ---------------- HV-15 ----------------

    [Fact(DisplayName = "HV-15: danh sách + checklist, cấp ngoại lệ / đủ điều kiện, cấp lần 2 bị chặn, gửi lại, tra cứu công khai")]
    public async Task Certificates()
    {
        var gv = await SignIn(GiaoVu);
        var rows = (await Ok(await gv.GetAsync($"{Students}/certificates", Ct))).EnumerateArray().ToList();
        rows.ShouldNotBeEmpty();
        rows.ShouldAllBe(r => r.GetProperty("status").GetString() == "completed");
        rows[0].GetProperty("checks").EnumerateArray().Select(c => c.GetProperty("key").GetString())
            .ShouldBe(["completed", "attendance", "final_exam", "tuition"]);
        var issuedBefore = rows.First(r => !Missing(r, "certificate"));
        issuedBefore.GetProperty("certificate").GetProperty("code").GetString()!.ShouldStartWith("CN-");

        var target = rows.FirstOrDefault(r => Missing(r, "certificate"));
        target.ValueKind.ShouldBe(JsonValueKind.Object, "Dữ liệu mẫu cần 1 HV hoàn thành lớp chưa được cấp chứng nhận");
        var eid = target.GetProperty("enrollmentId").GetGuid();
        var eligible = target.GetProperty("eligible").GetBoolean();
        var url = $"{Students}/enrollments/{eid}/certificate";

        await Expect(await gv.PostAsJsonAsync($"{url}/resend", new { email = "a@b.vn" }, Ct), HttpStatusCode.NotFound, "students.certificate.not_found");
        await Expect(await gv.PostAsJsonAsync(url, new { email = "sai-email" , exception = "Thi bù" }, Ct), HttpStatusCode.BadRequest,
            "students.certificate.email_required");
        if (!eligible)
            await Expect(await gv.PostAsJsonAsync(url, new { email = "hv.cn@gmail.com" }, Ct), HttpStatusCode.Conflict, "students.certificate.not_eligible");
        var issued = await Ok(await gv.PostAsJsonAsync(url, new { email = "HV.CN@gmail.com", exception = "Đã thi bù, giám đốc duyệt" }, Ct));
        issued.GetProperty("emailSent").GetBoolean().ShouldBeTrue();
        var cert = issued.GetProperty("enrollment").GetProperty("certificate");
        var code = cert.GetProperty("code").GetString()!;
        code.ShouldMatch(@"^CN-\d{4}-[A-Z0-9]+-\d{4}$");
        (cert.GetProperty("sentTo").GetString(), cert.GetProperty("sendCount").GetInt32()).ShouldBe(("hv.cn@gmail.com", 1));
        // Đủ điều kiện thì không lưu lý do ngoại lệ
        Missing(cert, "exception").ShouldBe(eligible);
        api.Emails.Last("hv.cn@gmail.com").Text.ShouldContain(code);

        await Expect(await gv.PostAsJsonAsync(url, new { email = "hv.cn@gmail.com", exception = "x" }, Ct), HttpStatusCode.Conflict,
            "students.certificate.already_issued");
        var resent = await Ok(await gv.PostAsJsonAsync($"{url}/resend", new { email = "phuhuynh.cn@gmail.com" }, Ct));
        resent.GetProperty("enrollment").GetProperty("certificate").GetProperty("sendCount").GetInt32().ShouldBe(2);
        api.Emails.Last("phuhuynh.cn@gmail.com").Subject.ShouldContain(code);

        // Tra cứu công khai — không cần đăng nhập, không lộ email
        var anon = api.CreateClient();
        var verify = await Ok(await anon.GetAsync($"{Students}/public/certificates/{code.ToLowerInvariant()}", Ct));
        (verify.GetProperty("code").GetString(), verify.GetProperty("studentName").GetString()).ShouldBe((code, target.GetProperty("studentName").GetString()));
        verify.TryGetProperty("sentTo", out _).ShouldBeFalse();
        await Expect(await anon.GetAsync($"{Students}/public/certificates/CN-1999-XX-0001", Ct), HttpStatusCode.NotFound, "students.certificate.invalid_code");

        // Ghi danh chưa hoàn thành: chặn kể cả có lý do ngoại lệ (E2)
        var st = await NewStudent(gv);
        var enrolled = await Ok(await gv.PostAsJsonAsync($"{Students}/enrollments", new { studentId = st, classId = ClassN5K21, discount = 0, dueDate = "2026-12-31" }, Ct));
        var studying = enrolled.GetProperty("enrollments")[0].GetProperty("id").GetGuid();
        await Expect(await gv.PostAsJsonAsync($"{Students}/enrollments/{studying}/certificate", new { email = "x@y.vn", exception = "Ngoại lệ" }, Ct),
            HttpStatusCode.Conflict, "students.certificate.not_completed");

        // Cấp hàng loạt (1 request): HV đủ điều kiện được cấp, HV đã cấp / chưa hoàn thành bị bỏ qua kèm lý do
        var others = rows.Where(r => Missing(r, "certificate") && r.GetProperty("eligible").GetBoolean() && r.GetProperty("enrollmentId").GetGuid() != eid)
            .Take(1).Select(r => r.GetProperty("enrollmentId").GetGuid()).ToList();
        var bulk = await Ok(await gv.PostAsJsonAsync($"{Students}/certificates/issue",
            new { items = others.Append(eid).Append(studying).Select((id, i) => new { enrollmentId = id, email = $"lo{i}@gmail.com" }) }, Ct));
        bulk.GetProperty("skipped").EnumerateArray().Select(x => x.GetProperty("code").GetString())
            .ShouldBe(["students.certificate.already_issued", "students.certificate.not_completed"], ignoreOrder: true);
        bulk.GetProperty("issued").GetArrayLength().ShouldBe(others.Count);
        foreach (var x in bulk.GetProperty("issued").EnumerateArray())
            x.GetProperty("enrollment").GetProperty("certificate").GetProperty("code").GetString().ShouldNotBe(code);

        var sale = await SignIn(Sale);
        await Expect(await sale.PostAsJsonAsync($"{Students}/certificates/issue", new { items = new[] { new { enrollmentId = eid, email = "a@b.vn" } } }, Ct),
            HttpStatusCode.Forbidden);
        await Expect(await sale.GetAsync($"{Students}/certificates", Ct), HttpStatusCode.Forbidden);
        await Expect(await sale.PostAsJsonAsync(url, new { email = "a@b.vn" }, Ct), HttpStatusCode.Forbidden);
    }

    // ---------------- HV-16 ----------------

    private static object Session(string label, string exam = "2027-07-04", string close = "2027-04-10", string result = "2027-08-25") => new
    {
        label, examDate = exam, regClose = close, resultDate = result,
        fees = new Dictionary<string, decimal> { ["N1"] = 700_000, ["N2"] = 650_000, ["N3"] = 600_000, ["N4"] = 550_000, ["N5"] = 550_000 },
        venues = new[] { "Hà Nội — ĐH Hà Nội", "TP.HCM — ĐH KHXH&NV" },
    };

    [Fact(DisplayName = "HV-16: kỳ thi (kiểm ngày / lệ phí), đăng ký nhiều HV (bỏ trùng), tiến độ → điểm tự chấm đỗ, chứng chỉ gốc về / phát, thống kê")]
    public async Task Jlpt()
    {
        var gv = await SignIn(GiaoVu);
        (await Ok(await gv.GetAsync($"{Students}/jlpt-sessions", Ct))).EnumerateArray()
            .ShouldContain(s => s.GetProperty("label").GetString() == "Tháng 12/2026" && s.GetProperty("fees").GetProperty("N1").GetDecimal() == 700_000);

        await Expect(await gv.PostAsJsonAsync($"{Students}/jlpt-sessions", Session("Kỳ sai", close: "2027-07-05"), Ct), HttpStatusCode.BadRequest,
            "students.jlpt_session.invalid_dates");
        var session = await Ok(await gv.PostAsJsonAsync($"{Students}/jlpt-sessions", Session($"Thử {Guid.NewGuid():N}"[..20]), Ct));
        var sid = session.GetProperty("id").GetGuid();
        session.GetProperty("closed").GetBoolean().ShouldBeFalse();
        await Expect(await gv.PostAsJsonAsync($"{Students}/jlpt-sessions", Session("Trùng ngày"), Ct), HttpStatusCode.Conflict, "students.jlpt_session.duplicate_date");

        var a = await NewStudent(gv);
        var b = await NewStudent(gv);
        await Expect(await gv.PostAsJsonAsync($"{Students}/jlpt-registrations", new { sessionId = sid, studentIds = new[] { a }, level = "N3", venue = "Đà Nẵng" }, Ct),
            HttpStatusCode.BadRequest, "students.jlpt.invalid_venue");
        var first = await Ok(await gv.PostAsJsonAsync($"{Students}/jlpt-registrations", new { sessionId = sid, studentIds = new[] { a }, level = "N3", venue = "Hà Nội — ĐH Hà Nội" }, Ct));
        var reg = first.GetProperty("created")[0];
        (reg.GetProperty("status").GetString(), reg.GetProperty("fee").GetDecimal()).ShouldBe(("registered", 600_000m));
        var again = await Ok(await gv.PostAsJsonAsync($"{Students}/jlpt-registrations", new { sessionId = sid, studentIds = new[] { a, b }, level = "N4", venue = "Hà Nội — ĐH Hà Nội" }, Ct));
        again.GetProperty("created").GetArrayLength().ShouldBe(1);
        again.GetProperty("skipped")[0].GetProperty("code").GetString().ShouldBe("students.jlpt.duplicate");

        var r = $"{Students}/jlpt-registrations/{reg.GetProperty("id").GetGuid()}";
        await Expect(await gv.PostAsync($"{r}/submit", null, Ct), HttpStatusCode.Conflict, "students.jlpt.invalid_status");
        (await Ok(await gv.PostAsync($"{r}/pay", null, Ct))).GetProperty("status").GetString().ShouldBe("paid");
        await Expect(await gv.PostAsync($"{r}/submit", null, Ct), HttpStatusCode.Conflict, "students.jlpt.docs_missing");
        await Ok(await gv.PutAsJsonAsync(r, new { level = "N3", venue = "Hà Nội — ĐH Hà Nội", docs = new { photo = true, idCard = true, form = true }, note = "Đủ hồ sơ" }, Ct));
        await Ok(await gv.PostAsync($"{r}/submit", null, Ct));
        await Expect(await gv.PutAsJsonAsync(r, new { level = "N2", venue = "Hà Nội — ĐH Hà Nội" }, Ct), HttpStatusCode.Conflict, "students.jlpt.invalid_status");
        await Ok(await gv.PostAsJsonAsync($"{r}/admit", new { admitNo = "N3-0001" }, Ct));
        await Expect(await gv.PostAsJsonAsync($"{r}/cert-received", new { no = "C1" }, Ct), HttpStatusCode.Conflict, "students.jlpt.not_passed");
        await Expect(await gv.PostAsJsonAsync($"{r}/result", new { scores = new[] { 40, 40 } }, Ct), HttpStatusCode.BadRequest, "students.jlpt.invalid_scores");
        var done = await Ok(await gv.PostAsJsonAsync($"{r}/result", new { scores = new[] { 40, 18, 50 } }, Ct));
        // Tổng 108 ≥ 95 nhưng Đọc hiểu bị liệt → trượt
        (done.GetProperty("status").GetString(), done.GetProperty("total").GetInt32(), done.GetProperty("passed").GetBoolean()).ShouldBe(("done", 108, false));
        done.GetProperty("failSections")[0].GetString().ShouldBe("Đọc hiểu");
        await Expect(await gv.PostAsync($"{r}/cancel", null, Ct), HttpStatusCode.Conflict, "students.jlpt.invalid_status");

        // Lượt của HV b: đỗ → chứng chỉ gốc về → phát 1 lần
        var rb = $"{Students}/jlpt-registrations/{again.GetProperty("created")[0].GetProperty("id").GetGuid()}";
        await Ok(await gv.PostAsync($"{rb}/pay", null, Ct));
        await Ok(await gv.PutAsJsonAsync(rb, new { level = "N4", venue = "Hà Nội — ĐH Hà Nội", docs = new { photo = true, idCard = true, form = true } }, Ct));
        await Ok(await gv.PostAsync($"{rb}/submit", null, Ct));
        await Ok(await gv.PostAsJsonAsync($"{rb}/admit", new { admitNo = "N4-0002" }, Ct));
        (await Ok(await gv.PostAsJsonAsync($"{rb}/result", new { scores = new[] { 80, 40 } }, Ct))).GetProperty("passed").GetBoolean().ShouldBeTrue();
        await Expect(await gv.PostAsJsonAsync($"{rb}/cert-handed", new { handedTo = "Mẹ HV" }, Ct), HttpStatusCode.Conflict, "students.jlpt.cert_not_received");
        await Ok(await gv.PostAsJsonAsync($"{rb}/cert-received", new { no = "JL-2027-123" }, Ct));
        var handed = await Ok(await gv.PostAsJsonAsync($"{rb}/cert-handed", new { handedTo = "Mẹ HV" }, Ct));
        handed.GetProperty("certificate").GetProperty("handedTo").GetString().ShouldBe("Mẹ HV");
        handed.GetProperty("certificate").GetProperty("receivedBy").GetString().ShouldNotBe("—");
        await Expect(await gv.PostAsJsonAsync($"{rb}/cert-handed", new { handedTo = "Bố HV" }, Ct), HttpStatusCode.Conflict, "students.jlpt.cert_already_handed");

        var stats = await Ok(await gv.GetAsync($"{Students}/jlpt-registrations/stats?sessionId={sid}", Ct));
        var n3 = stats.GetProperty("byLevel").EnumerateArray().Single(x => x.GetProperty("key").GetString() == "N3");
        (n3.GetProperty("sat").GetInt32(), n3.GetProperty("passed").GetInt32(), n3.GetProperty("passRate").GetInt32()).ShouldBe((1, 0, 0));
        stats.GetProperty("byLevel").EnumerateArray().Single(x => x.GetProperty("key").GetString() == "N4").GetProperty("passRate").GetInt32().ShouldBe(100);

        // Quá hạn nhận hồ sơ: cảnh báo, vẫn cho (E6)
        var late = await Ok(await gv.PostAsJsonAsync($"{Students}/jlpt-sessions", Session($"Muộn {Guid.NewGuid():N}"[..20], "2026-12-20", "2026-01-01", "2027-02-01"), Ct));
        var lateReg = await Ok(await gv.PostAsJsonAsync($"{Students}/jlpt-registrations", new { sessionId = late.GetProperty("id").GetGuid(), studentIds = new[] { a }, level = "N5", venue = "Hà Nội — ĐH Hà Nội" }, Ct));
        (lateReg.GetProperty("created").GetArrayLength(), lateReg.GetProperty("warnings").GetArrayLength()).ShouldBe((1, 1));

        var regs = await Ok(await gv.GetAsync($"{Students}/jlpt-registrations?sessionId={sid}", Ct));
        regs.GetArrayLength().ShouldBe(2);

        // Hàng loạt (1 request): đóng phí rồi nộp hội đồng — lượt sai thứ tự / thiếu giấy tờ bị bỏ qua kèm lý do
        var c = await NewStudent(gv);
        var d = await NewStudent(gv);
        var two = (await Ok(await gv.PostAsJsonAsync($"{Students}/jlpt-registrations", new { sessionId = sid, studentIds = new[] { c, d }, level = "N5", venue = "Hà Nội — ĐH Hà Nội" }, Ct)))
            .GetProperty("created").EnumerateArray().Select(x => x.GetProperty("id").GetGuid()).ToList();
        var paid = await Ok(await gv.PostAsJsonAsync($"{Students}/jlpt-registrations/pay", new { ids = two.Append(reg.GetProperty("id").GetGuid()) }, Ct));
        (paid.GetProperty("updated").GetArrayLength(), paid.GetProperty("skipped")[0].GetProperty("code").GetString()).ShouldBe((2, "students.jlpt.invalid_status"));
        await Ok(await gv.PutAsJsonAsync($"{Students}/jlpt-registrations/{two[0]}", new { level = "N5", venue = "Hà Nội — ĐH Hà Nội", docs = new { photo = true, idCard = true, form = true } }, Ct));
        var submitted = await Ok(await gv.PostAsJsonAsync($"{Students}/jlpt-registrations/submit", new { ids = two }, Ct));
        submitted.GetProperty("updated")[0].GetProperty("status").GetString().ShouldBe("submitted");
        submitted.GetProperty("skipped")[0].GetProperty("code").GetString().ShouldBe("students.jlpt.docs_missing");

        var sale = await SignIn(Sale);
        await Expect(await sale.GetAsync($"{Students}/jlpt-registrations", Ct), HttpStatusCode.Forbidden);
        await Expect(await sale.PostAsync($"{r}/pay", null, Ct), HttpStatusCode.Forbidden);
        await Expect(await sale.PostAsJsonAsync($"{Students}/jlpt-registrations/submit", new { ids = two }, Ct), HttpStatusCode.Forbidden);
    }

    // ---------------- HV-17 ----------------

    [Fact(DisplayName = "HV-17: tạo hồ sơ (checklist theo chương trình), đi từng bước có điều kiện, mốc ngày, phí, dừng / mở lại, sửa đè 409")]
    public async Task Abroad()
    {
        var gv = await SignIn(GiaoVu);
        (await Ok(await gv.GetAsync($"{Students}/abroad-cases", Ct))).GetArrayLength().ShouldBeGreaterThan(0);

        var st = await NewStudent(gv);
        object Body(string intake = "2027-04", decimal fee = 150_000_000, uint? rowVersion = null) => new
        {
            studentId = st, program = "study", partner = "Trường Nhật ngữ ABC", destination = "Tokyo", intake, jlptRequired = "N4", feeTotal = fee,
            assigneeId = EmployeeHa, rowVersion,
        };
        await Expect(await gv.PostAsJsonAsync($"{Students}/abroad-cases", Body(intake: "2027/04"), Ct), HttpStatusCode.BadRequest, "students.abroad.invalid_intake");
        var c = await Ok(await gv.PostAsJsonAsync($"{Students}/abroad-cases", Body(), Ct));
        var id = c.GetProperty("id").GetGuid();
        var url = $"{Students}/abroad-cases/{id}";
        (c.GetProperty("stage").GetString(), c.GetProperty("docs").GetArrayLength(), c.GetProperty("notes")[0].GetProperty("text").GetString())
            .ShouldBe(("consult", 7, "Tạo hồ sơ."));
        c.GetProperty("assignee").GetString().ShouldNotBe("—");

        (await Ok(await gv.PostAsync($"{url}/advance", null, Ct))).GetProperty("stage").GetString().ShouldBe("documents");
        await Expect(await gv.PostAsync($"{url}/advance", null, Ct), HttpStatusCode.Conflict, "students.abroad.docs_not_verified");
        JsonElement cur = c;
        foreach (var doc in c.GetProperty("docs").EnumerateArray())
            cur = await Ok(await gv.PutAsJsonAsync($"{url}/documents/{doc.GetProperty("id").GetGuid()}", new { state = "verified" }, Ct));
        (await Ok(await gv.PostAsync($"{url}/advance", null, Ct))).GetProperty("stage").GetString().ShouldBe("interview");
        await Expect(await gv.PostAsync($"{url}/advance", null, Ct), HttpStatusCode.Conflict, "students.abroad.interview_not_passed");
        await Expect(await gv.PutAsJsonAsync($"{url}/milestones", new { interviewDate = "2026-12-01", interviewResult = "pass", coeSubmittedAt = "2026-12-10", coeGrantedAt = "2026-12-05" }, Ct),
            HttpStatusCode.BadRequest, "students.abroad.invalid_dates");
        var ms = await Ok(await gv.PutAsJsonAsync($"{url}/milestones", new { interviewDate = "2026-12-01", interviewResult = "pass" }, Ct));
        ms.GetProperty("interview").GetProperty("result").GetString().ShouldBe("pass");
        (await Ok(await gv.PostAsync($"{url}/advance", null, Ct))).GetProperty("stage").GetString().ShouldBe("coe");
        await Expect(await gv.PostAsync($"{url}/advance", null, Ct), HttpStatusCode.Conflict, "students.abroad.coe_missing");

        await Expect(await gv.PostAsJsonAsync($"{url}/payments", new { amount = 200_000_000 }, Ct), HttpStatusCode.BadRequest, "students.abroad.invalid_payment");
        (await Ok(await gv.PostAsJsonAsync($"{url}/payments", new { amount = 50_000_000 }, Ct))).GetProperty("fee").GetProperty("paid").GetDecimal().ShouldBe(50_000_000m);

        // Dừng (cần lý do) → thao tác bị chặn, ghi chú vẫn được → mở lại giữ bước
        await Expect(await gv.PostAsJsonAsync($"{url}/cancel", new { text = " " }, Ct), HttpStatusCode.BadRequest, "students.abroad.reason_required");
        var cancelled = await Ok(await gv.PostAsJsonAsync($"{url}/cancel", new { text = "Gia đình đổi kế hoạch" }, Ct));
        cancelled.GetProperty("cancelled").GetProperty("reason").GetString().ShouldBe("Gia đình đổi kế hoạch");
        Ids(await Ok(await gv.GetAsync($"{Students}/abroad-cases", Ct))).ShouldNotContain(id);
        Ids(await Ok(await gv.GetAsync($"{Students}/abroad-cases?includeCancelled=true&studentId={st}", Ct))).ShouldContain(id);
        await Expect(await gv.PostAsync($"{url}/advance", null, Ct), HttpStatusCode.Conflict, "students.abroad.cancelled");
        await Ok(await gv.PostAsJsonAsync($"{url}/notes", new { text = "Gọi lại sau Tết" }, Ct));
        var reopened = await Ok(await gv.PostAsync($"{url}/reopen", null, Ct));
        reopened.GetProperty("stage").GetString().ShouldBe("coe");
        reopened.GetProperty("notes").EnumerateArray().Select(n => n.GetProperty("text").GetString()).ShouldContain("Mở lại hồ sơ.");

        // Sửa thông tin chung: tổng phí < đã đóng bị chặn; sửa đè 409
        var v = reopened.GetProperty("rowVersion").GetUInt32();
        await Expect(await gv.PutAsJsonAsync(url, Body(fee: 10_000_000, rowVersion: v), Ct), HttpStatusCode.BadRequest, "students.abroad.invalid_payment");
        var edited = await Ok(await gv.PutAsJsonAsync(url, Body(intake: "2027-10", rowVersion: v), Ct));
        edited.GetProperty("intake").GetString().ShouldBe("2027-10");
        await Expect(await gv.PutAsJsonAsync(url, Body(rowVersion: v), Ct), HttpStatusCode.Conflict);

        var sale = await SignIn(Sale);
        await Expect(await sale.GetAsync($"{Students}/abroad-cases", Ct), HttpStatusCode.Forbidden);
        await Expect(await sale.PostAsync($"{url}/advance", null, Ct), HttpStatusCode.Forbidden);
    }

    // ---------------- Cổng HV / PH ----------------

    [Fact(DisplayName = "Cổng HV / PH: chứng nhận của mình (không lộ thông tin nội bộ), JLPT của mình, tự đăng ký kỳ còn nhận hồ sơ, PH chọn con")]
    public async Task LearnerPortal()
    {
        var student = await SignIn(StudentAcc);
        var certs = await Ok(await student.GetAsync($"{Portal}/certificates", Ct));
        certs.GetArrayLength().ShouldBeGreaterThan(0);
        foreach (var c in certs.EnumerateArray())
        {
            c.GetProperty("studentId").GetGuid().ShouldBe(St1);
            c.GetProperty("checks").GetArrayLength().ShouldBe(4);
            if (!Missing(c, "certificate"))
            {
                Missing(c.GetProperty("certificate"), "exception").ShouldBeTrue();
                Missing(c.GetProperty("certificate"), "sentBy").ShouldBeTrue();
            }
        }

        // Kỳ mới còn nhận hồ sơ → HV tự đăng ký; ghi chú nội bộ của trung tâm không hiện ở cổng
        var gv = await SignIn(GiaoVu);
        var open = (await Ok(await gv.PostAsJsonAsync($"{Students}/jlpt-sessions", Session($"Cổng {Guid.NewGuid():N}"[..20], "2027-12-05", "2027-08-20", "2028-01-25"), Ct))).GetProperty("id").GetGuid();
        var mine = await Ok(await student.PostAsJsonAsync($"{Portal}/jlpt-registrations", new { sessionId = open, level = "N4", venue = "Hà Nội — ĐH Hà Nội" }, Ct));
        (mine.GetProperty("status").GetString(), mine.GetProperty("level").GetString(), mine.GetProperty("studentId").GetGuid()).ShouldBe(("registered", "N4", St1));
        await Expect(await student.PostAsJsonAsync($"{Portal}/jlpt-registrations", new { sessionId = open, level = "N3", venue = "Hà Nội — ĐH Hà Nội" }, Ct),
            HttpStatusCode.Conflict, "students.jlpt.duplicate");
        var regUrl = $"{Students}/jlpt-registrations/{mine.GetProperty("id").GetGuid()}";
        await Ok(await gv.PutAsJsonAsync(regUrl, new { level = "N4", venue = "Hà Nội — ĐH Hà Nội", docs = new { photo = true, idCard = false, form = false }, note = "Nội bộ: gọi nhắc CCCD" }, Ct));

        var jlpt = await Ok(await student.GetAsync($"{Portal}/jlpt", Ct));
        jlpt.GetProperty("sessions").EnumerateArray().ShouldContain(s => s.GetProperty("id").GetGuid() == open);
        var reg = jlpt.GetProperty("registrations").EnumerateArray().Single(r => r.GetProperty("sessionId").GetGuid() == open);
        reg.GetProperty("docs").GetProperty("photo").GetBoolean().ShouldBeTrue();
        Missing(reg, "note").ShouldBeTrue();

        // Kỳ đã hết hạn nhận hồ sơ: cổng không cho tự đăng ký (trang nhân viên vẫn được)
        var closed = (await Ok(await gv.PostAsJsonAsync($"{Students}/jlpt-sessions", Session($"Hết {Guid.NewGuid():N}"[..20], "2026-12-13", "2026-01-01", "2027-02-01"), Ct)))
            .GetProperty("id").GetGuid();
        await Expect(await student.PostAsJsonAsync($"{Portal}/jlpt-registrations", new { sessionId = closed, level = "N4", venue = "Hà Nội — ĐH Hà Nội" }, Ct),
            HttpStatusCode.Conflict, "students.jlpt.registration_closed");

        // PH nhiều con phải chọn con; con người khác → không thấy
        var parent = await SignIn(ParentAcc);
        await Expect(await parent.GetAsync($"{Portal}/jlpt", Ct), HttpStatusCode.NotFound, "students.student.not_found");
        (await Ok(await parent.GetAsync($"{Portal}/jlpt?studentId={St1}", Ct))).GetProperty("registrations").EnumerateArray()
            .ShouldContain(r => r.GetProperty("sessionId").GetGuid() == open);
        await Expect(await parent.GetAsync($"{Portal}/certificates?studentId={await NewStudent(gv)}", Ct), HttpStatusCode.NotFound, "students.student.not_found");

        // Nhân viên không dùng được API cổng
        await Expect(await gv.GetAsync($"{Portal}/certificates", Ct), HttpStatusCode.Forbidden);
    }

    /// <summary>API bỏ trường null khỏi JSON</summary>
    private static bool Missing(JsonElement e, string name) => !e.TryGetProperty(name, out var v) || v.ValueKind == JsonValueKind.Null;

    private static List<Guid> Ids(JsonElement list) => list.EnumerateArray().Select(x => x.GetProperty("id").GetGuid()).ToList();
}
