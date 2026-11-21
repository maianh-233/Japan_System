using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text.Json;
using JPCenter.Infrastructure.Common.Persistence;
using JPCenter.IntegrationTests.Infrastructure;
using JPCenter.Modules.Academic.Contracts;
using JPCenter.Modules.Academic.Contracts.Dtos;
using JPCenter.Modules.Academic.Domain.Questions;
using JPCenter.Modules.Academic.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;

namespace JPCenter.IntegrationTests.Academic;

/// <summary>User story 11 — Ngân hàng câu hỏi qua API + PostgreSQL thật với dữ liệu mẫu.</summary>
[Collection(SeededCollection.Name)]
public sealed class QuestionFlowTests(SeededApiFactory api)
{
    private const string Admin = "admin@jpcenter.vn", Staff = "giaovu@jpcenter.vn", Sale = "sale@jpcenter.vn";
    private const string Base = "/api/v1/academic/questions";
    private static readonly Guid N5Curriculum = SeedData.IdOf("CUR-N5-V2"), N3Curriculum = SeedData.IdOf("CUR-N3-V1");
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

    private static async Task Expect(HttpResponseMessage r, HttpStatusCode status, string? code = null)
    {
        var body = await r.Content.ReadAsStringAsync(Ct);
        r.StatusCode.ShouldBe(status, body);
        if (code is not null) JsonDocument.Parse(body).RootElement.GetProperty("code").GetString().ShouldBe(code);
    }

    private static string Unique() => $"{DateTime.UtcNow:HHmmssfff}{Random.Shared.Next(100, 999)}";

    private static object Body(string stem, string skill = "grammar", string format = "choice", string[]? options = null, string level = "N5",
        Guid? curriculumId = null, Guid[]? lessonIds = null, string? passage = null, bool submit = false, uint rowVersion = 0) => new
    {
        level, skill, format, difficulty = "easy", stem, passage, transcript = skill == "listening" ? "男：…" : null,
        options = options ?? ["が", "を", "に", "で"], answer = 1, explanation = "Giải thích", curriculumId, lessonIds = lessonIds ?? [],
        tags = new[] { "Thử" }, submit, rowVersion,
    };

    private async Task<Guid[]> N5Lessons()
    {
        await using var scope = api.Scope();
        var c = await scope.ServiceProvider.GetRequiredService<AcademicDbContext>().Curricula.AsNoTracking()
            .FirstAsync(x => x.Id == new Modules.Academic.Domain.Curricula.CurriculumId(N5Curriculum), Ct);
        return c.Lessons.OrderBy(l => l.Order).Select(l => l.Id.Value).ToArray();
    }

    // ---------- NH-01 → NH-03, NH-05 ----------

    [Fact(DisplayName = "NH-01→05: soạn (phạm vi own), kiểm tra nội dung, gửi duyệt, không tự duyệt, sửa câu đã duyệt về chờ duyệt, chống sửa đè")]
    public async Task Create_review_update()
    {
        var staff = await SignIn(Staff);
        var admin = await SignIn(Admin);
        var lessons = await N5Lessons();

        // Kiểm tra nội dung
        await Expect(await staff.PostAsJsonAsync(Base, Body("x（　）", options: ["a", "b", "c"]), Ct), HttpStatusCode.BadRequest, "academic.question.options");
        await Expect(await staff.PostAsJsonAsync(Base, Body("không có sao", format: "star"), Ct), HttpStatusCode.BadRequest, "academic.question.star_missing");
        await Expect(await staff.PostAsJsonAsync(Base, Body("x（　）", curriculumId: N3Curriculum), Ct), HttpStatusCode.BadRequest,
            "academic.question.lesson_invalid"); // lộ trình N3 cho câu N5
        await Expect(await staff.PostAsJsonAsync(Base, Body("x（　）", curriculumId: N5Curriculum, lessonIds: [Guid.NewGuid()]), Ct),
            HttpStatusCode.BadRequest, "academic.question.lesson_invalid");

        var stem = "Câu thử " + Unique() + "（　）";
        var created = await Ok(await staff.PostAsJsonAsync(Base, Body(stem, curriculumId: N5Curriculum, lessonIds: [lessons[2]]), Ct));
        var id = created.GetProperty("id").GetGuid();
        created.GetProperty("status").GetString().ShouldBe("draft");
        created.GetProperty("code").GetString()!.ShouldStartWith("N5-GR-");
        created.GetProperty("level").GetString().ShouldBe("N5");
        created.GetProperty("author").GetString().ShouldBe("Vũ Ngọc Mai");

        // Phạm vi own: giáo vụ chỉ thấy câu mình soạn; admin thấy cả ngân hàng
        var mine = (await Ok(await staff.GetAsync(Base, Ct))).EnumerateArray().ToList();
        mine.ShouldContain(q => q.GetProperty("id").GetGuid() == id);
        mine.ShouldAllBe(q => q.GetProperty("author").GetString() == "Vũ Ngọc Mai");
        (await Ok(await admin.GetAsync(Base + "?level=N5&skill=grammar", Ct))).EnumerateArray()
            .ShouldContain(q => q.GetProperty("id").GetGuid() == id);
        await Expect(await staff.GetAsync($"{Base}/{SeedData.IdOf("Q-1")}", Ct), HttpStatusCode.NotFound, "academic.question.not_found");
        (await Ok(await admin.GetAsync(Base + "/stats", Ct))).GetProperty("approved").GetInt32().ShouldBeGreaterThanOrEqualTo(30);

        // Gửi duyệt; giáo vụ không có quyền duyệt
        (await Ok(await staff.PostAsJsonAsync(Base + "/submit", new { ids = new[] { id } }, Ct))).GetProperty("done")[0]
            .GetProperty("status").GetString().ShouldBe("review");
        await Expect(await staff.PostAsJsonAsync(Base + "/approve", new { ids = new[] { id } }, Ct), HttpStatusCode.Forbidden);

        // Trả lại cần lý do; admin duyệt được
        await Expect(await admin.PostAsJsonAsync(Base + "/return", new { ids = new[] { id } }, Ct), HttpStatusCode.BadRequest, "validation.failed");
        var approved = await Ok(await admin.PostAsJsonAsync(Base + "/approve", new { ids = new[] { id, Guid.NewGuid() } }, Ct));
        approved.GetProperty("done")[0].GetProperty("status").GetString().ShouldBe("approved");
        approved.GetProperty("done")[0].GetProperty("review").GetProperty("by").GetString().ShouldBe("Phạm Thu Trang");
        approved.GetProperty("skipped").GetArrayLength().ShouldBe(1);

        // Sửa câu đã duyệt → chờ duyệt; bản cũ → 409
        var detail = await Ok(await staff.GetAsync($"{Base}/{id}", Ct));
        var version = detail.GetProperty("rowVersion").GetUInt32();
        var edited = await Ok(await staff.PutAsJsonAsync($"{Base}/{id}", Body(stem + "！", curriculumId: N5Curriculum, lessonIds: [lessons[2]],
            rowVersion: version), Ct));
        edited.GetProperty("status").GetString().ShouldBe("review");
        await Expect(await staff.PutAsJsonAsync($"{Base}/{id}", Body(stem, rowVersion: version), Ct), HttpStatusCode.Conflict);

        // Admin tự soạn rồi tự duyệt → bỏ qua (không tự duyệt)
        var own = await Ok(await admin.PostAsJsonAsync(Base, Body("Câu admin " + Unique() + "（　）", submit: true), Ct));
        var self = await Ok(await admin.PostAsJsonAsync(Base + "/approve", new { ids = new[] { own.GetProperty("id").GetGuid() } }, Ct));
        self.GetProperty("done").GetArrayLength().ShouldBe(0);
        self.GetProperty("skipped")[0].GetProperty("reason").GetString()!.ShouldContain("không tự duyệt");

        await Expect(await (await SignIn(Sale)).GetAsync(Base, Ct), HttpStatusCode.Forbidden);
    }

    // ---------- NH-04, NH-06, NH-07 ----------

    [Fact(DisplayName = "NH-04/06/07: file nghe (chỉ nghe hiểu, gửi duyệt cần file, link nghe thử), ngừng dùng / mở lại, nhân bản")]
    public async Task Audio_retire_duplicate()
    {
        var admin = await SignIn(Admin);
        var staff = await SignIn(Staff);

        var listening = await Ok(await staff.PostAsJsonAsync(Base, Body("Nghe " + Unique() + "（　）", skill: "listening"), Ct));
        var id = listening.GetProperty("id").GetGuid();
        await Expect(await staff.PostAsJsonAsync(Base, Body("Nghe " + Unique(), skill: "listening", submit: true), Ct), HttpStatusCode.BadRequest,
            "academic.question.audio_required");
        (await Ok(await staff.PostAsJsonAsync(Base + "/submit", new { ids = new[] { id } }, Ct))).GetProperty("skipped").GetArrayLength().ShouldBe(1);

        using var form = new MultipartFormDataContent();
        var file = new ByteArrayContent([.. "ID3"u8, 0x04, .. new byte[200]]);
        file.Headers.ContentType = new MediaTypeHeaderValue("audio/mpeg");
        form.Add(file, "file", "bai-nghe.mp3");
        form.Add(new StringContent("75"), "durationSec");
        var withAudio = await Ok(await staff.PostAsync($"{Base}/{id}/audio", form, Ct));
        withAudio.GetProperty("audio").GetProperty("name").GetString().ShouldBe("bai-nghe.mp3");
        withAudio.GetProperty("audio").GetProperty("durationSec").GetInt32().ShouldBe(75);
        (await Ok(await staff.GetAsync($"{Base}/{id}/audio-link", Ct))).GetProperty("url").GetString().ShouldNotBeNullOrEmpty();

        // Câu không phải nghe hiểu → không nhận file
        var grammar = (await Ok(await staff.PostAsJsonAsync(Base, Body("Ngữ pháp " + Unique() + "（　）"), Ct))).GetProperty("id").GetGuid();
        using var form2 = new MultipartFormDataContent();
        var file2 = new ByteArrayContent([.. "ID3"u8, 0x04, .. new byte[200]]);
        file2.Headers.ContentType = new MediaTypeHeaderValue("audio/mpeg");
        form2.Add(file2, "file", "x.mp3");
        form2.Add(new StringContent("10"), "durationSec");
        await Expect(await staff.PostAsync($"{Base}/{grammar}/audio", form2, Ct), HttpStatusCode.BadRequest, "academic.question.audio_not_listening");

        (await Ok(await staff.PostAsJsonAsync(Base + "/submit", new { ids = new[] { id } }, Ct))).GetProperty("done").GetArrayLength().ShouldBe(1);
        await Ok(await admin.PostAsJsonAsync(Base + "/approve", new { ids = new[] { id } }, Ct));

        // Ngừng dùng cần lý do → mở lại về chờ duyệt
        var retired = await Ok(await admin.PostAsJsonAsync(Base + "/retire", new { ids = new[] { id }, note = "Lộ đề" }, Ct));
        retired.GetProperty("done")[0].GetProperty("status").GetString().ShouldBe("retired");
        var detail = await Ok(await admin.GetAsync($"{Base}/{id}", Ct));
        await Expect(await staff.PutAsJsonAsync($"{Base}/{id}", Body("Sửa", skill: "listening", rowVersion: detail.GetProperty("rowVersion").GetUInt32()), Ct),
            HttpStatusCode.Conflict, "academic.question.retired");
        (await Ok(await admin.PostAsJsonAsync(Base + "/reopen", new { ids = new[] { id } }, Ct))).GetProperty("done")[0].GetProperty("status").GetString()
            .ShouldBe("review");

        // Nhân bản (dùng chung file nghe)
        var copy = await Ok(await admin.PostAsync($"{Base}/{id}/duplicate", null, Ct));
        copy.GetProperty("status").GetString().ShouldBe("draft");
        copy.GetProperty("author").GetString().ShouldBe("Phạm Thu Trang");
        copy.GetProperty("audio").GetProperty("name").GetString().ShouldBe("bai-nghe.mp3");
        copy.GetProperty("code").GetString().ShouldNotBe(listening.GetProperty("code").GetString());
    }

    // ---------- NH-08 ----------

    [Fact(DisplayName = "NH-08: nhập nhiều câu 1 request — dòng đúng tạo, dòng sai báo theo số dòng, nghe hiểu vào nháp")]
    public async Task Import_rows()
    {
        var staff = await SignIn(Staff);
        var u = Unique();
        object Row(int row, string stem, string skill = "vocab", string[]? options = null) => new
        {
            row, level = "N4", skill, format = "choice", difficulty = "medium", stem, options = options ?? ["あ", "い", "う", "え"], answer = 0,
            lessonIds = Array.Empty<Guid>(), tags = new[] { "Excel" },
        };

        var r = await Ok(await staff.PostAsJsonAsync(Base + "/import", new
        {
            submit = true,
            rows = new[] { Row(2, "Excel 1 " + u), Row(3, "Excel 2 " + u, options: ["あ", "あ", "う", "え"]), Row(4, "Excel 3 " + u, skill: "listening") },
        }, Ct));
        var created = r.GetProperty("created").EnumerateArray().ToList();
        created.Count.ShouldBe(2);
        created.Single(c => c.GetProperty("skill").GetString() == "vocab").GetProperty("status").GetString().ShouldBe("review");
        created.Single(c => c.GetProperty("skill").GetString() == "listening").GetProperty("status").GetString().ShouldBe("draft");
        r.GetProperty("draftListening").GetInt32().ShouldBe(1);
        r.GetProperty("errors")[0].GetProperty("row").GetInt32().ShouldBe(3);

        await Expect(await staff.PostAsJsonAsync(Base + "/import", new { submit = false, rows = Array.Empty<object>() }, Ct), HttpStatusCode.BadRequest);
    }

    // ---------- NH-09, NH-10 ----------

    [Fact(DisplayName = "NH-09/10: bốc câu đã duyệt theo tiêu chí (không đáp án), tăng số đề đã dùng, chấm + ghi lượt làm")]
    public async Task Picker_for_exams()
    {
        await using var scope = api.Scope();
        var picker = scope.ServiceProvider.GetRequiredService<IQuestionPicker>();
        var db = scope.ServiceProvider.GetRequiredService<AcademicDbContext>();

        var pick = await picker.PickAsync(new QuestionPickCriteria("N5", ["grammar", "kanji"], null, null, [], 50), Ct);
        pick.Questions.Count.ShouldBeGreaterThan(0);
        pick.Missing.ShouldBe(50 - pick.Questions.Count);
        var ids = pick.Questions.Select(q => new QuestionId(q.Id)).ToList();
        var rows = await db.Questions.AsNoTracking().Where(q => ids.Contains(q.Id)).ToListAsync(Ct);
        rows.ShouldAllBe(q => q.Status == QuestionStatus.Approved && q.Level == SharedKernel.Primitives.JlptLevel.N5);
        rows.ShouldAllBe(q => q.Skill == SharedKernel.Primitives.JapaneseSkill.Grammar || q.Skill == SharedKernel.Primitives.JapaneseSkill.Kanji);

        // Loại trừ câu đã có
        var again = await picker.PickAsync(new QuestionPickCriteria("N5", ["grammar", "kanji"], null, null, [], 50, pick.Questions.Select(q => q.Id).ToList()), Ct);
        again.Questions.ShouldBeEmpty();

        // Chấm: đáp án + ghi lượt làm
        var first = pick.Questions[0].Id;
        var before = await db.Questions.AsNoTracking().FirstAsync(q => q.Id == new QuestionId(first), Ct);
        var answers = await picker.AnswersAsync([first], Ct);
        await picker.RecordAnswersAsync([new QuestionAnswer(first, answers[first]), new QuestionAnswer(first, -1)], Ct);
        var after = await db.Questions.AsNoTracking().FirstAsync(q => q.Id == new QuestionId(first), Ct);
        (after.AttemptCount - before.AttemptCount, after.CorrectCount - before.CorrectCount).ShouldBe((2, 1));
        after.UsedCount.ShouldBe(before.UsedCount); // đã tăng lúc bốc, chấm không tăng nữa

        (await picker.GetAsync([first], Ct)).Single().Options.Count.ShouldBe(4);
    }
}
