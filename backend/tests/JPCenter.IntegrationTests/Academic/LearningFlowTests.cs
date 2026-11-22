using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text;
using System.Text.Json;
using JPCenter.Application.Common.Messaging;
using JPCenter.Infrastructure.Common.Persistence;
using JPCenter.IntegrationTests.Infrastructure;
using JPCenter.Modules.Academic.Application.Announcements.DeliverScheduled;
using JPCenter.Modules.Academic.Application.Exams.AutoSubmitExams;
using JPCenter.Modules.Academic.Contracts;
using JPCenter.Modules.Academic.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;

namespace JPCenter.IntegrationTests.Academic;

/// <summary>
/// User story 12 (giai đoạn 20 phase 3) — bài tập upload / quiz, kỳ thi online / giấy, kết quả & công bố, thông báo lớp (hẹn giờ), đánh giá GV,
/// flashcard, tài liệu GV (Library), job tự nộp / gửi hẹn giờ, số liệu học tập cho cảnh báo bỏ học.
/// </summary>
[Collection(SeededCollection.Name)]
public sealed class LearningFlowTests(SeededApiFactory api)
{
    private const string Academic = "/api/v1/academic";
    private const string Teaching = "/api/v1/academic/portal/teaching";
    private const string Learning = "/api/v1/academic/portal/learning";
    private const string Library = "/api/v1/library/portal";
    private static CancellationToken Ct => TestContext.Current.CancellationToken;
    private static int _seq;

    private static readonly Guid Cg = SeedData.IdOf("cg");
    private static readonly Guid N5Course = SeedData.IdOf("CRS-01");
    private static readonly Guid N5Curriculum = SeedData.IdOf("CUR-N5-V2");
    private static readonly Guid TeacherHa = SeedData.IdOf("t-ha");
    private static readonly Guid St1 = SeedData.IdOf("ST-1");
    private static readonly Guid Q11 = SeedData.IdOf("Q-11");

    private const string Staff = "giaovu@jpcenter.vn", Ha = "giaovien@jpcenter.vn", Long = "long.t.long@jpcenter.vn";
    private const string StudentAcc = "hocvien@jpcenter.vn", ParentAcc = "phuhuynh@jpcenter.vn";

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

    private static string Suffix() => $"L{Interlocked.Increment(ref _seq)}{Random.Shared.Next(100, 999)}";

    /// <summary>Lớp N5 (lộ trình CUR-N5-V2) GV Hà, học online Chủ nhật xa — mỗi test 1 lớp riêng, HV mẫu ST-1 ghi danh</summary>
    private static async Task<Guid> OpenClass(HttpClient staff, string start, string end)
    {
        var code = "GVV_N5-" + Suffix();
        var r = await staff.PostAsJsonAsync($"{Academic}/classes", new
        {
            courseId = N5Course, curriculumId = N5Curriculum, code, name = "Lớp nội dung học " + code, branchId = Cg, teacherIds = new[] { TeacherHa },
            assistantIds = Array.Empty<Guid>(), roomId = (Guid?)null, online = true, meetingUrl = "https://meet.example.com/" + code,
            plannedStartDate = new DateOnly(2027, 8, 1), plannedEndDate = (DateOnly?)null, weekdays = new[] { 7 }, startTime = start, endTime = end,
            totalHours = 10m, minStudents = 1, maxStudents = 15, fee = 4_900_000m, previousClassId = (Guid?)null, note = (string?)null,
            generateSessions = false, applyToUpcoming = false, rowVersion = (uint?)null,
        }, Ct);
        r.StatusCode.ShouldBe(HttpStatusCode.Created, await r.Content.ReadAsStringAsync(Ct));
        var id = (await Json(r)).GetProperty("class").GetProperty("id").GetGuid();
        await Ok(await staff.PostAsJsonAsync("/api/v1/students/enrollments", new { studentId = St1, classId = id }, Ct));
        return id;
    }

    /// <summary>Bài 6 của lộ trình N5 (Id bài học sinh khi nạp mẫu — lấy qua API)</summary>
    private static async Task<Guid> Lesson6(HttpClient staff) =>
        (await Ok(await staff.GetAsync($"{Academic}/curricula/{N5Curriculum}", Ct))).GetProperty("lessons")[5].GetProperty("id").GetGuid();

    /// <summary>Mốc thi giữa kỳ / cuối kỳ của lộ trình lớp</summary>
    private static async Task<(Guid Mid, Guid Final)> Milestones(HttpClient staff, Guid classId)
    {
        var list = (await Ok(await staff.GetAsync($"{Academic}/classes/{classId}/exams", Ct))).GetProperty("milestones").EnumerateArray().ToList();
        Guid Of(string kind) => list.First(m => m.GetProperty("kind").GetString() == kind).GetProperty("id").GetGuid();
        return (Of("midterm"), Of("final"));
    }

    private static object OnlineMidterm(Guid midterm, DateTimeOffset startsAt, int kanji = 2, int grammar = 2) => new
    {
        examId = midterm, title = "Kiểm tra giữa kỳ", mode = "online", startsAt, durationMinutes = 30, passPercent = 60m, shuffle = true,
        sections = new object[]
        {
            new { name = "Hán tự", skill = "kanji", points = 2m, count = kanji },
            new { name = "Ngữ pháp", skill = "grammar", points = 3m, count = grammar },
        },
    };

    private async Task Sql(FormattableString sql)
    {
        await using var scope = api.Scope();
        await scope.ServiceProvider.GetRequiredService<AcademicDbContext>().Database.ExecuteSqlInterpolatedAsync(sql, Ct);
    }

    private async Task<int> Run<T>(T command) where T : ICommand<int>
    {
        await using var scope = api.Scope();
        var r = await scope.ServiceProvider.GetRequiredService<ISender>().Send(command, Ct);
        r.IsSuccess.ShouldBeTrue(r.IsFailure ? r.Error.Message : null);
        return r.Value;
    }

    private static JsonElement ById(JsonElement list, Guid id, string prop = "id") =>
        list.EnumerateArray().Single(x => x.GetProperty(prop).GetGuid() == id);

    // ---------------- Bài tập ----------------

    [Fact(DisplayName = "ND-01→03: GV giao bài upload, HV tải tệp / nộp / nộp lại, GV chấm bước 0,25; quiz bốc câu đã duyệt, không lộ đáp án, giữ lượt cao nhất")]
    public async Task Assignments_upload_and_quiz()
    {
        var staff = await SignIn(Staff);
        var ha = await SignIn(Ha);
        var student = await SignIn(StudentAcc);
        var classId = await OpenClass(staff, "06:00", "07:30");
        var L6 = await Lesson6(staff);
        var due = DateTimeOffset.UtcNow.AddDays(3);

        // GV khác / nhân viên gọi cổng GV / GV gọi trang nhân viên → chặn
        await Expect(await (await SignIn(Long)).GetAsync($"{Teaching}/classes/{classId}/assignments", Ct), HttpStatusCode.Forbidden,
            "academic.class.not_teaching");
        (await ha.GetAsync($"{Academic}/classes/{classId}/assignments", Ct)).StatusCode.ShouldBe(HttpStatusCode.Forbidden);

        // ND-01: upload có đề đính kèm (link) — E1 hạn nộp trong quá khứ
        await Expect(await ha.PostAsJsonAsync($"{Teaching}/classes/{classId}/assignments",
            new { type = "upload", title = "Viết đoạn văn", dueDate = DateTimeOffset.UtcNow.AddDays(-1) }, Ct),
            HttpStatusCode.BadRequest, "academic.assignment.invalid_due");
        var upload = await Ok(await ha.PostAsJsonAsync($"{Teaching}/classes/{classId}/assignments", new
        {
            type = "upload", title = "Viết đoạn văn tự giới thiệu", lessonId = L6, dueDate = due,
            attachments = new[] { new { name = "Đề bài", kind = "link", url = "https://example.com/de-bai", size = (long?)null } },
        }, Ct));
        var uploadId = upload.GetProperty("assignment").GetProperty("id").GetGuid();
        upload.GetProperty("assignment").GetProperty("maxScore").GetDecimal().ShouldBe(10m);
        (await Ok(await student.GetAsync($"{Learning}/assignments/{uploadId}/attachments/0/link", Ct))).GetProperty("url").GetString()
            .ShouldBe("https://example.com/de-bai");

        // ND-02: tải tệp → nộp; khóa tệp tùy ý bị từ chối; nộp lại trước khi chấm
        using var form = new MultipartFormDataContent();
        form.Add(new ByteArrayContent(Encoding.ASCII.GetBytes("%PDF-1.4\nbai lam")), "file", "bai-lam.pdf");
        var file = await Ok(await student.PostAsync($"{Learning}/files", form, Ct));
        file.GetProperty("kind").GetString().ShouldBe("pdf");
        await Expect(await student.PostAsJsonAsync($"{Learning}/assignments/{uploadId}/submissions",
            new { files = new[] { new { name = "x.pdf", kind = "pdf", url = "academic/other/x.pdf", size = 10 } } }, Ct),
            HttpStatusCode.BadRequest, "academic.file.invalid");
        await Expect(await student.PostAsJsonAsync($"{Learning}/assignments/{uploadId}/submissions", new { content = "  " }, Ct),
            HttpStatusCode.BadRequest, "academic.submission.empty");
        await Ok(await student.PostAsJsonAsync($"{Learning}/assignments/{uploadId}/submissions",
            new { files = new[] { JsonSerializer.Deserialize<object>(file.GetRawText()) } }, Ct));
        var sub = await Ok(await student.PostAsJsonAsync($"{Learning}/assignments/{uploadId}/submissions",
            new { files = new[] { JsonSerializer.Deserialize<object>(file.GetRawText()) }, content = "Em nộp lại" }, Ct));
        var subId = sub.GetProperty("id").GetGuid();
        (sub.GetProperty("content").GetString(), sub.GetProperty("late").GetBoolean()).ShouldBe(("Em nộp lại", false));
        (await Ok(await ha.GetAsync($"{Teaching}/assignments/{uploadId}/submissions/{subId}/files/0/link", Ct))).GetProperty("url").GetString()
            .ShouldStartWith("https://files.test/academic/learning/");

        // ND-03: bước 0,25; đã chấm thì HV không nộp lại được
        await Expect(await ha.PutAsJsonAsync($"{Teaching}/submissions/{subId}/grade", new { score = 8.3m }, Ct),
            HttpStatusCode.BadRequest, "academic.submission.invalid_score");
        var graded = await Ok(await ha.PutAsJsonAsync($"{Teaching}/submissions/{subId}/grade", new { score = 8.25m, comment = "Tốt" }, Ct));
        graded.GetProperty("score").GetDecimal().ShouldBe(8.25m);
        graded.GetProperty("gradedBy").GetString().ShouldNotBeNullOrEmpty("Tên GV chấm");
        await Expect(await student.PostAsJsonAsync($"{Learning}/assignments/{uploadId}/submissions", new { content = "Sửa" }, Ct),
            HttpStatusCode.Conflict, "academic.submission.already_graded");

        // ND-01 quiz: bài 6 chỉ có 2 câu ngữ pháp đã duyệt → cảnh báo, vẫn giao (E4)
        var quiz = await Ok(await staff.PostAsJsonAsync($"{Academic}/classes/{classId}/assignments", new
        {
            type = "quiz", title = "Trắc nghiệm ngữ pháp bài 6", dueDate = due, maxScore = 10m,
            quiz = new { lessonIds = new[] { L6 }, skills = new[] { "grammar" }, difficulty = "mixed", questionCount = 3, attempts = 2, shuffle = true },
        }, Ct));
        quiz.GetProperty("warning").GetString()!.ShouldStartWith("Chỉ có 2 câu");
        var quizId = quiz.GetProperty("assignment").GetProperty("id").GetGuid();

        var mine = await Ok(await student.GetAsync($"{Learning}/assignments", Ct));
        var row = mine.EnumerateArray().Single(x => x.GetProperty("assignment").GetProperty("id").GetGuid() == quizId);
        (row.GetProperty("attemptsLeft").GetInt32(), row.GetProperty("canSubmit").GetBoolean()).ShouldBe((2, true));
        (await (await SignIn(ParentAcc)).PostAsync($"{Learning}/assignments/{quizId}/quiz/start", null, Ct)).StatusCode
            .ShouldBe(HttpStatusCode.Forbidden);

        // Phát đề: không có đáp án; nộp → chấm ở server, trả đáp án + giải thích
        var paper1 = await Ok(await student.PostAsync($"{Learning}/assignments/{quizId}/quiz/start", null, Ct));
        var questions = paper1.GetProperty("questions");
        questions.GetArrayLength().ShouldBe(3);
        paper1.GetRawText().ShouldNotContain("\"answer\"");
        var token1 = paper1.GetProperty("token").GetString();
        var answers = questions.EnumerateArray().Select(q => new { questionId = q.GetProperty("id").GetGuid(), choice = (int?)0 }).ToList();
        var result1 = await Ok(await student.PostAsJsonAsync($"{Learning}/assignments/{quizId}/quiz/submit", new { token = token1, answers }, Ct));
        result1.GetProperty("total").GetInt32().ShouldBe(3);
        result1.GetProperty("review").EnumerateArray().All(r => r.TryGetProperty("answer", out _)).ShouldBeTrue();
        result1.GetProperty("submission").GetProperty("quiz").GetProperty("attempts").GetInt32().ShouldBe(1);

        // 1 vé chỉ nộp 1 lần; hết lượt → chặn
        var paper2 = await Ok(await student.PostAsync($"{Learning}/assignments/{quizId}/quiz/start", null, Ct));
        await Expect(await student.PostAsJsonAsync($"{Learning}/assignments/{quizId}/quiz/submit", new { token = token1, answers }, Ct),
            HttpStatusCode.Conflict, "academic.quiz.ticket_used");
        var result2 = await Ok(await student.PostAsJsonAsync($"{Learning}/assignments/{quizId}/quiz/submit",
            new { token = paper2.GetProperty("token").GetString(), answers = Array.Empty<object>() }, Ct));
        (result2.GetProperty("correct").GetInt32(), result2.GetProperty("submission").GetProperty("quiz").GetProperty("attempts").GetInt32())
            .ShouldBe((0, 2));
        result2.GetProperty("submission").GetProperty("score").GetDecimal().ShouldBe(result1.GetProperty("score").GetDecimal(), "Giữ lần cao nhất");
        await Expect(await student.PostAsync($"{Learning}/assignments/{quizId}/quiz/start", null, Ct),
            HttpStatusCode.Conflict, "academic.submission.no_attempts_left");

        // ND-01.E6: đã có bài nộp → không đổi tiêu chí; vẫn gia hạn được
        await Expect(await staff.PutAsJsonAsync($"{Academic}/assignments/{quizId}", new
        {
            type = "quiz", title = "Trắc nghiệm ngữ pháp bài 6", dueDate = due, maxScore = 20m,
            quiz = new { lessonIds = new[] { L6 }, skills = new[] { "grammar" }, difficulty = "mixed", questionCount = 3, attempts = 2, shuffle = true },
        }, Ct), HttpStatusCode.Conflict, "academic.assignment.has_submissions");
        await Ok(await staff.PutAsJsonAsync($"{Academic}/assignments/{quizId}", new
        {
            type = "quiz", title = "Trắc nghiệm bài 6 (gia hạn)", dueDate = due.AddDays(2), maxScore = 10m,
            quiz = new { lessonIds = new[] { L6 }, skills = new[] { "grammar" }, difficulty = "mixed", questionCount = 3, attempts = 2, shuffle = true },
        }, Ct));

        // Bảng giáo vụ: đủ bài + bài nộp; xóa mềm (E7)
        var board = await Ok(await staff.GetAsync($"{Academic}/classes/{classId}/assignments", Ct));
        (board.GetProperty("assignments").GetArrayLength(), board.GetProperty("submissions").GetArrayLength()).ShouldBe((2, 2));
        (await staff.DeleteAsync($"{Academic}/assignments/{uploadId}", Ct)).StatusCode.ShouldBe(HttpStatusCode.NoContent);
        (await Ok(await staff.GetAsync($"{Academic}/classes/{classId}/assignments", Ct))).GetProperty("assignments").GetArrayLength().ShouldBe(1);

        // HV-14: bài quá hạn mà chưa nộp được tính
        await Sql($"update academic.assignments set assigned_at = now() - interval '10 days', due_date = now() - interval '1 day' where id = {quizId}");
        await using var scope = api.Scope();
        var stat = (await scope.ServiceProvider.GetRequiredService<ILearningStats>().ForClassesAsync([classId], [St1], Ct)).Single();
        (stat.Due, stat.Submitted, stat.AssignmentAvgPct is not null).ShouldBe((1, 1, true));
    }

    // ---------------- Kỳ thi ----------------

    [Fact(DisplayName = "ND-04→06: kỳ thi online từ mốc lộ trình (đề riêng, tự lưu, nộp, công bố, xem lại) + thi giấy nhập điểm / vắng thi")]
    public async Task Online_and_paper_exams()
    {
        var staff = await SignIn(Staff);
        var ha = await SignIn(Ha);
        var student = await SignIn(StudentAcc);
        var classId = await OpenClass(staff, "08:00", "09:30");
        var (Midterm, Final) = await Milestones(staff, classId);

        var board = await Ok(await staff.GetAsync($"{Academic}/classes/{classId}/exams", Ct));
        var mid = ById(board.GetProperty("milestones"), Midterm);
        mid.GetProperty("lessonIds").GetArrayLength().ShouldBe(8, "Giữa kỳ: bài 1 → 8");

        // E2 thiếu câu; xem trước đề có đáp án
        var notEnough = await staff.PostAsJsonAsync($"{Academic}/classes/{classId}/exams/draw",
            new { examId = Midterm, sections = new[] { new { name = "Ngữ pháp", skill = "grammar", points = 1m, count = 10 } } }, Ct);
        await Expect(notEnough, HttpStatusCode.BadRequest, "academic.exam.not_enough_questions");
        var preview = await Ok(await staff.PostAsJsonAsync($"{Academic}/classes/{classId}/exams/draw",
            new { examId = Midterm, sections = new[] { new { name = "Hán tự", skill = "kanji", points = 1m, count = 2 } } }, Ct));
        preview[0].GetProperty("questions")[0].TryGetProperty("answer", out _).ShouldBeTrue();

        // Tạo nháp → HV chưa thấy; trùng mốc → E1
        var exam = await Ok(await staff.PostAsJsonAsync($"{Academic}/classes/{classId}/exams", OnlineMidterm(Midterm, DateTimeOffset.UtcNow.AddMinutes(-1)), Ct));
        var examId = exam.GetProperty("id").GetGuid();
        (exam.GetProperty("status").GetString(), exam.GetProperty("questionCount").GetInt32(), exam.GetProperty("maxScore").GetDecimal())
            .ShouldBe(("draft", 4, 10m));
        await Expect(await staff.PostAsJsonAsync($"{Academic}/classes/{classId}/exams", OnlineMidterm(Midterm, DateTimeOffset.UtcNow), Ct),
            HttpStatusCode.Conflict, "academic.exam.duplicate");
        (await Ok(await student.GetAsync($"{Learning}/exams", Ct))).EnumerateArray()
            .ShouldNotContain(x => x.GetProperty("exam").GetProperty("id").GetGuid() == examId);

        // Phát hành → HV vào thi: đề riêng không đáp án, tự lưu, mở lại tiếp tục
        await Ok(await staff.PostAsync($"{Academic}/exams/{examId}/publish", null, Ct));
        var listedExam = (await Ok(await student.GetAsync($"{Learning}/exams", Ct))).EnumerateArray()
            .Single(x => x.GetProperty("exam").GetProperty("id").GetGuid() == examId);
        (listedExam.GetProperty("canStart").GetBoolean(), listedExam.GetProperty("exam").GetProperty("sections")[0].GetProperty("questionIds")
            .GetArrayLength()).ShouldBe((true, 0), "Cổng HV không nhận danh sách câu");
        var paper = await Ok(await student.PostAsync($"{Learning}/exams/{examId}/start", null, Ct));
        var attemptId = paper.GetProperty("attemptId").GetGuid();
        paper.GetProperty("questions").GetArrayLength().ShouldBe(4);
        paper.GetRawText().ShouldNotContain("\"answer\"");
        var first = paper.GetProperty("questions")[0].GetProperty("id").GetGuid();
        (await student.PutAsJsonAsync($"{Learning}/exam-attempts/{attemptId}/answers",
            new { answers = new[] { new { questionId = first, choice = (int?)1 } } }, Ct)).StatusCode.ShouldBe(HttpStatusCode.NoContent);
        var resumed = await Ok(await student.PostAsync($"{Learning}/exams/{examId}/start", null, Ct));
        (resumed.GetProperty("attemptId").GetGuid(), resumed.GetProperty("answers")[0].GetProperty("choice").GetInt32()).ShouldBe((attemptId, 1));

        // Chưa hết giờ → chưa công bố; đã có bài làm → không hủy
        await Expect(await staff.PostAsync($"{Academic}/exams/{examId}/publish-results", null, Ct), HttpStatusCode.Conflict, "academic.exam.not_finished");
        await Expect(await staff.PostAsync($"{Academic}/exams/{examId}/cancel", null, Ct), HttpStatusCode.Conflict, "academic.exam.has_attempts");

        // Nộp 1 lần; chưa công bố → cổng không có điểm
        var submitted = await Ok(await student.PostAsync($"{Learning}/exam-attempts/{attemptId}/submit", null, Ct));
        (submitted.GetProperty("answered").GetInt32(), submitted.GetProperty("total").GetInt32()).ShouldBe((1, 4));
        await Expect(await student.PostAsync($"{Learning}/exam-attempts/{attemptId}/submit", null, Ct),
            HttpStatusCode.Conflict, "academic.exam.already_submitted");
        var mineBefore = (await Ok(await student.GetAsync($"{Learning}/exams", Ct))).EnumerateArray()
            .Single(x => x.GetProperty("exam").GetProperty("id").GetGuid() == examId);
        (mineBefore.GetProperty("attempt").GetString(), Absent(mineBefore, "result")).ShouldBe(("submitted", true));
        await Expect(await student.GetAsync($"{Learning}/exams/{examId}/review", Ct), HttpStatusCode.Conflict, "academic.exam.results_not_published");

        // GV xem kết quả lớp: HV đã nộp có điểm tự chấm theo 2 phần
        var results = await Ok(await ha.GetAsync($"{Teaching}/exams/{examId}/results", Ct));
        var st1Row = ById(results.GetProperty("rows"), St1, "studentId");
        st1Row.GetProperty("attempt").GetString().ShouldBe("submitted");
        st1Row.GetProperty("result").GetProperty("sections").GetArrayLength().ShouldBe(2);

        // Hết giờ thi → công bố; HV thấy điểm + xem lại đáp án; sửa điểm sau công bố vẫn được (E2)
        await Sql($"update academic.class_exams set starts_at = now() - interval '2 hours' where id = {examId}");
        await Ok(await staff.PostAsync($"{Academic}/exams/{examId}/publish-results", null, Ct));
        var mineAfter = (await Ok(await student.GetAsync($"{Learning}/exams", Ct))).EnumerateArray()
            .Single(x => x.GetProperty("exam").GetProperty("id").GetGuid() == examId);
        mineAfter.GetProperty("result").GetProperty("maxScore").GetDecimal().ShouldBe(10m);
        var review = await Ok(await student.GetAsync($"{Learning}/exams/{examId}/review", Ct));
        review.GetProperty("questions").GetArrayLength().ShouldBe(4);
        ById(review.GetProperty("questions"), first, "questionId").GetProperty("chosen").GetInt32().ShouldBe(1);
        await Expect(await ha.PutAsJsonAsync($"{Teaching}/exams/{examId}/results/{St1}", new { absent = false, scores = new[] { 5m, 6m } }, Ct),
            HttpStatusCode.BadRequest, "academic.exam.invalid_score");
        var edited = await Ok(await ha.PutAsJsonAsync($"{Teaching}/exams/{examId}/results/{St1}",
            new { absent = false, scores = new[] { 4m, 3m }, comment = "Bù câu lỗi" }, Ct));
        (edited.GetProperty("totalScore").GetDecimal(), edited.GetProperty("percent").GetDecimal()).ShouldBe((7m, 70m));

        // Thi giấy (cuối kỳ): HV không làm online; GV nhập điểm / vắng thi
        var paperExam = await Ok(await staff.PostAsJsonAsync($"{Academic}/classes/{classId}/exams", new
        {
            examId = Final, title = "Thi cuối kỳ", mode = "paper", startsAt = DateTimeOffset.UtcNow.AddDays(1), durationMinutes = 60,
            shuffle = false, sections = new[] { new { name = "Nghe", skill = "listening", points = 5m, questionIds = new[] { Q11 } } },
        }, Ct));
        var paperId = paperExam.GetProperty("id").GetGuid();
        await Expect(await ha.PutAsJsonAsync($"{Teaching}/exams/{paperId}/results/{St1}", new { absent = false, scores = new[] { 5m } }, Ct),
            HttpStatusCode.Conflict, "academic.exam.not_published");
        await Ok(await staff.PostAsync($"{Academic}/exams/{paperId}/publish", null, Ct));
        (await Ok(await ha.GetAsync($"{Teaching}/exams/{paperId}/paper", Ct))).GetProperty("sections")[0].GetProperty("questions")[0]
            .GetProperty("answer").ValueKind.ShouldBe(JsonValueKind.Number);
        await Expect(await student.PostAsync($"{Learning}/exams/{paperId}/start", null, Ct), HttpStatusCode.Conflict, "academic.exam.paper");
        var absent = await Ok(await ha.PutAsJsonAsync($"{Teaching}/exams/{paperId}/results/{St1}", new { absent = true, comment = "Ốm" }, Ct));
        (absent.GetProperty("status").GetString(), Absent(absent, "percent")).ShouldBe(("absent", true));
        await Ok(await ha.PostAsync($"{Teaching}/exams/{paperId}/publish-results", null, Ct));

        // HV-14: 1 bài thi tốt (70%) + 1 vắng thi
        await using var scope = api.Scope();
        var stat = (await scope.ServiceProvider.GetRequiredService<ILearningStats>().ForClassesAsync([classId], [St1], Ct)).Single();
        stat.ExamPercents.ShouldBe([70, null], ignoreOrder: true);
    }

    [Fact(DisplayName = "ND-05.E4: hết giờ mà HV chưa nộp → job tự nộp các câu đã lưu")]
    public async Task Expired_attempt_is_auto_submitted()
    {
        var staff = await SignIn(Staff);
        var student = await SignIn(StudentAcc);
        var classId = await OpenClass(staff, "10:00", "11:30");
        var (midterm, _) = await Milestones(staff, classId);
        var examId = (await Ok(await staff.PostAsJsonAsync($"{Academic}/classes/{classId}/exams",
            OnlineMidterm(midterm, DateTimeOffset.UtcNow.AddMinutes(-1), kanji: 1, grammar: 1), Ct))).GetProperty("id").GetGuid();
        await Ok(await staff.PostAsync($"{Academic}/exams/{examId}/publish", null, Ct));
        var paper = await Ok(await student.PostAsync($"{Learning}/exams/{examId}/start", null, Ct));
        var attemptId = paper.GetProperty("attemptId").GetGuid();
        await student.PutAsJsonAsync($"{Learning}/exam-attempts/{attemptId}/answers",
            new { answers = new[] { new { questionId = paper.GetProperty("questions")[0].GetProperty("id").GetGuid(), choice = (int?)0 } } }, Ct);

        await Sql($"update academic.exam_attempts set deadline_at = now() - interval '5 minutes' where id = {attemptId}");
        (await Run(new AutoSubmitExamsCommand())).ShouldBeGreaterThanOrEqualTo(1);
        var row = ById((await Ok(await staff.GetAsync($"{Academic}/exams/{examId}/results", Ct))).GetProperty("rows"), St1, "studentId");
        row.GetProperty("attempt").GetString().ShouldBe("submitted");
        row.GetProperty("result").GetProperty("status").GetString().ShouldBe("done");
    }

    // ---------------- Thông báo, đánh giá GV, flashcard, tài liệu GV ----------------

    [Fact(DisplayName = "ND-07: thông báo lớp gửi ngay / hẹn giờ (job gửi) / hủy khi còn chờ; HV xem đếm 1 lần")]
    public async Task Class_announcements()
    {
        var staff = await SignIn(Staff);
        var ha = await SignIn(Ha);
        var student = await SignIn(StudentAcc);
        var classId = await OpenClass(staff, "12:00", "13:30");
        object Body(DateTimeOffset? at, string title = "Nghỉ lễ") => new
        {
            title, content = "Lớp nghỉ ngày 2/9, học bù sau.", audience = new[] { "student", "parent" }, channels = new[] { "portal" },
            debtorsOnly = false, scheduledAt = at,
        };

        var sent = await Ok(await ha.PostAsJsonAsync($"{Teaching}/classes/{classId}/announcements", Body(null), Ct));
        (sent.GetProperty("status").GetString(), sent.GetProperty("recipients").GetInt32() >= 1).ShouldBe(("sent", true));
        var id = sent.GetProperty("id").GetGuid();
        var mine = ById(await Ok(await student.GetAsync($"{Learning}/announcements", Ct)), id);
        mine.GetProperty("read").GetBoolean().ShouldBeFalse();
        (await student.PostAsync($"{Learning}/announcements/{id}/read", null, Ct)).StatusCode.ShouldBe(HttpStatusCode.NoContent);
        await student.PostAsync($"{Learning}/announcements/{id}/read", null, Ct);
        ById(await Ok(await staff.GetAsync($"{Academic}/classes/{classId}/announcements", Ct)), id).GetProperty("readCount").GetInt32().ShouldBe(1);

        // E2 hẹn giờ quá khứ · E3 chỉ hủy khi còn chờ
        await Expect(await staff.PostAsJsonAsync($"{Academic}/classes/{classId}/announcements", Body(DateTimeOffset.UtcNow.AddMinutes(-5)), Ct),
            HttpStatusCode.BadRequest, "academic.announcement.invalid_schedule");
        await Expect(await staff.DeleteAsync($"{Academic}/announcements/{id}", Ct), HttpStatusCode.Conflict, "academic.announcement.not_pending");
        var later = await Ok(await staff.PostAsJsonAsync($"{Academic}/classes/{classId}/announcements", Body(DateTimeOffset.UtcNow.AddHours(1), "Hủy"), Ct));
        later.GetProperty("status").GetString().ShouldBe("scheduled");
        (await Ok(await staff.DeleteAsync($"{Academic}/announcements/{later.GetProperty("id").GetGuid()}", Ct))).GetProperty("status").GetString()
            .ShouldBe("cancelled");

        // Tới giờ hẹn → job gửi
        var scheduled = await Ok(await staff.PostAsJsonAsync($"{Academic}/classes/{classId}/announcements",
            Body(DateTimeOffset.UtcNow.AddHours(1), "Hẹn giờ"), Ct));
        var scheduledId = scheduled.GetProperty("id").GetGuid();
        await Sql($"update academic.class_announcements set scheduled_at = now() - interval '1 minute' where id = {scheduledId}");
        (await Run(new DeliverScheduledAnnouncementsCommand())).ShouldBeGreaterThanOrEqualTo(1);
        ById(await Ok(await student.GetAsync($"{Learning}/announcements", Ct)), scheduledId).GetProperty("title").GetString().ShouldBe("Hẹn giờ");
    }

    [Fact(DisplayName = "ND-08 / 09 / 10: HV đánh giá GV (ẩn danh với GV), flashcard soạn + ôn Leitner, tài liệu GV gửi lớp mình dạy")]
    public async Task Reviews_flashcards_and_teacher_documents()
    {
        var staff = await SignIn(Staff);
        var ha = await SignIn(Ha);
        var student = await SignIn(StudentAcc);
        var classId = await OpenClass(staff, "14:00", "15:30");
        var (midterm, _) = await Milestones(staff, classId);
        var L6 = await Lesson6(staff);

        // ND-08: chưa có thi giữa kỳ → chưa mở khảo sát; thi giữa kỳ hôm nay → mở đợt giữa kỳ
        var review = new
        {
            classId, teacherId = TeacherHa, overall = 5, ratings = new { method = 5, clarity = 4, enthusiasm = 5, punctuality = 5 },
            comment = "Cô dạy dễ hiểu", anonymous = true,
        };
        await Expect(await student.PostAsJsonAsync($"{Learning}/teacher-reviews", review, Ct), HttpStatusCode.Conflict, "academic.teacher_review.not_open");
        var examId = (await Ok(await staff.PostAsJsonAsync($"{Academic}/classes/{classId}/exams",
            OnlineMidterm(midterm, DateTimeOffset.UtcNow.AddMinutes(-1), kanji: 1, grammar: 1), Ct))).GetProperty("id").GetGuid();
        await Ok(await staff.PostAsync($"{Academic}/exams/{examId}/publish", null, Ct));
        var survey = (await Ok(await student.GetAsync($"{Learning}/teacher-reviews/surveys", Ct))).EnumerateArray()
            .Single(x => x.GetProperty("classId").GetGuid() == classId);
        survey.GetProperty("period").GetString().ShouldBe("mid");
        await Ok(await student.PostAsJsonAsync($"{Learning}/teacher-reviews", review, Ct));
        await Expect(await student.PostAsJsonAsync($"{Learning}/teacher-reviews", review, Ct), HttpStatusCode.Conflict, "academic.teacher_review.duplicate");
        var forTeacher = (await Ok(await ha.GetAsync($"{Teaching}/reviews", Ct))).EnumerateArray().First(x => x.GetProperty("classId").GetGuid() == classId);
        Absent(forTeacher, "studentId").ShouldBeTrue("Ẩn danh → GV không thấy HV");
        (await Ok(await staff.GetAsync($"{Academic}/classes/{classId}/teacher-reviews", Ct)))[0].GetProperty("studentId").GetGuid().ShouldBe(St1);

        // ND-09: soạn bộ thẻ bài 6, sửa / xóa / sắp xếp; HV ôn
        await Expect(await staff.PostAsJsonAsync($"{Academic}/flash-decks", new
        {
            title = "Thiếu nghĩa", level = "n5", kind = "vocab", lessonId = L6, cards = new[] { new { front = "水" } },
        }, Ct), HttpStatusCode.BadRequest, "academic.flashcard.card_required");
        var deck = await Ok(await staff.PostAsJsonAsync($"{Academic}/flash-decks", new
        {
            title = "Từ vựng bài 6 " + Suffix(), level = "n5", kind = "vocab", lessonId = L6,
            cards = new[] { new { front = "水", reading = "みず", meaning = "nước" }, new { front = "火", reading = "ひ", meaning = "lửa" } },
        }, Ct));
        var deckId = deck.GetProperty("id").GetGuid();
        deck = await Ok(await staff.PostAsJsonAsync($"{Academic}/flash-decks/{deckId}/cards",
            new { cards = new[] { new { front = "木", meaning = "cây" } } }, Ct));
        var cards = deck.GetProperty("cards").EnumerateArray().Select(c => c.GetProperty("id").GetGuid()).ToList();
        cards.Count.ShouldBe(3);
        deck = await Ok(await staff.PutAsJsonAsync($"{Academic}/flash-decks/{deckId}/cards/order", new { cardIds = cards.AsEnumerable().Reverse() }, Ct));
        deck.GetProperty("cards")[0].GetProperty("front").GetString().ShouldBe("木");

        var mine = await Ok(await student.GetAsync($"{Learning}/flashcards", Ct));
        ById(mine.GetProperty("decks"), deckId).GetProperty("cards").GetArrayLength().ShouldBe(3);
        var reviewed = await Ok(await student.PostAsJsonAsync($"{Learning}/flashcards/reviews",
            new { reviews = new[] { new { deckId, cardId = cards[0], remembered = true }, new { deckId, cardId = cards[1], remembered = false } } }, Ct));
        var p0 = ById(reviewed.GetProperty("progress"), cards[0], "cardId");
        (p0.GetProperty("box").GetInt32(), ById(reviewed.GetProperty("progress"), cards[1], "cardId").GetProperty("lapses").GetInt32()).ShouldBe((1, 1));
        reviewed.GetProperty("streak").GetInt32().ShouldBeGreaterThanOrEqualTo(1);
        var orphan = await Ok(await staff.PostAsJsonAsync($"{Academic}/flash-decks",
            new { title = "Không gắn bài " + Suffix(), level = "n5", kind = "kanji", cards = new[] { new { front = "山", meaning = "núi" } } }, Ct));
        await Expect(await student.PostAsJsonAsync($"{Learning}/flashcards/reviews", new
        {
            reviews = new[] { new { deckId = orphan.GetProperty("id").GetGuid(), cardId = orphan.GetProperty("cards")[0].GetProperty("id").GetGuid(), remembered = true } },
        }, Ct), HttpStatusCode.Forbidden, "academic.flashcard.not_available");
        // E2: xóa thẻ đã ôn → xóa cả tiến độ
        await Ok(await staff.DeleteAsync($"{Academic}/flash-decks/{deckId}/cards/{cards[0]}", Ct));
        (await Ok(await student.GetAsync($"{Learning}/flashcards", Ct))).GetProperty("progress").EnumerateArray()
            .ShouldNotContain(p => p.GetProperty("cardId").GetGuid() == cards[0]);

        // ND-10: tài liệu GV — chỉ lớp mình dạy; HV mở → lượt xem; người khác không sửa được
        await Expect(await ha.PostAsJsonAsync($"{Library}/teaching/documents", new
        {
            title = "Từ vựng bổ sung", file = new { name = "Link", kind = "link", url = "https://example.com/tv" }, classIds = new[] { Guid.NewGuid() },
        }, Ct), HttpStatusCode.Forbidden, "library.teacher_doc.class_not_allowed");
        var doc = await Ok(await ha.PostAsJsonAsync($"{Library}/teaching/documents", new
        {
            title = "Từ vựng bổ sung", description = "Ôn trước buổi sau", file = new { name = "Link", kind = "link", url = "https://example.com/tv" },
            classIds = new[] { classId },
        }, Ct));
        var docId = doc.GetProperty("id").GetGuid();
        var listed = ById(await Ok(await student.GetAsync($"{Library}/learning/documents", Ct)), docId);
        listed.GetProperty("viewed").GetBoolean().ShouldBeFalse();
        (await Ok(await student.GetAsync($"{Library}/learning/documents/{docId}/link", Ct))).GetProperty("url").GetString().ShouldBe("https://example.com/tv");
        ById(await Ok(await ha.GetAsync($"{Library}/teaching/documents", Ct)), docId).GetProperty("viewedBy").EnumerateArray()
            .Select(x => x.GetGuid()).ShouldBe([St1]);
        await Expect(await (await SignIn(Long)).PutAsJsonAsync($"{Library}/teaching/documents/{docId}",
            new { title = "Sửa", file = new { name = "Link", kind = "link", url = "https://example.com/tv" }, classIds = new[] { classId } }, Ct),
            HttpStatusCode.Forbidden, "library.teacher_doc.not_owner");
        (await student.GetAsync($"{Library}/teaching/documents", Ct)).StatusCode.ShouldBe(HttpStatusCode.Forbidden);
    }
}
