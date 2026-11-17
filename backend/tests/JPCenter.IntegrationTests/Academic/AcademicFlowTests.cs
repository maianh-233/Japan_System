using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text;
using System.Text.Json;
using JPCenter.Infrastructure.Common.Persistence;
using JPCenter.IntegrationTests.Infrastructure;
using JPCenter.Modules.Academic.Domain.Curricula;
using JPCenter.Modules.Academic.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;

namespace JPCenter.IntegrationTests.Academic;

/// <summary>User story 09 — khóa học & lộ trình, trên API + PostgreSQL thật với dữ liệu mẫu (kho file giả trong bộ nhớ).</summary>
[Collection(SeededCollection.Name)]
public sealed class AcademicFlowTests(SeededApiFactory api)
{
    private const string Courses = "/api/v1/academic/courses";
    private const string Curricula = "/api/v1/academic/curricula";
    private static CancellationToken Ct => TestContext.Current.CancellationToken;
    private static int _seq;

    private static readonly Guid Cg = SeedData.IdOf("cg");
    private static readonly Guid Q1 = SeedData.IdOf("q1");
    private static readonly Guid N5Course = SeedData.IdOf("CRS-01");
    private static readonly Guid N5Curriculum = SeedData.IdOf("CUR-N5-V2");
    private static readonly Guid N3Draft = SeedData.IdOf("CUR-N3-V2");

    private static readonly byte[] Pdf = Encoding.ASCII.GetBytes("%PDF-1.4\n% bai doc\n%%EOF");

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

    private static string NewCode(string prefix) => $"{prefix}-{Interlocked.Increment(ref _seq)}{Random.Shared.Next(1000, 9999)}";

    private static object NewCourse(string code, string format = "group", int? maxStudents = 15, Guid? cloneFromId = null, uint? rowVersion = null,
        decimal fee = 5_000_000) =>
        new
        {
            code, name = "Khóa thử " + code, category = "jlpt", levelFrom = "N4", levelTo = "N3", format, teacherType = "vn", mode = "offline",
            sessions = 40, minutesPerSession = 120, sessionsPerWeek = 2, fee, maxStudents, branchIds = new[] { Cg, Q1 },
            description = (string?)null, cloneFromId, rowVersion,
        };

    private static object NewCurriculum(string code, string tier = "standard", string? entry = null, Guid? cloneFromId = null, string version = "v1.0") =>
        new { code, name = "Lộ trình thử " + code, level = "N3", tier, entryRequirement = entry, version, textbook = "Shin Kanzen",
            description = (string?)null, tags = new[] { "JLPT" }, cloneFromId, rowVersion = (uint?)null };

    private static MultipartFormDataContent FileForm(byte[] bytes, string fileName, string? name = null, bool shared = true)
    {
        var form = new MultipartFormDataContent();
        var file = new ByteArrayContent(bytes);
        file.Headers.ContentType = new MediaTypeHeaderValue("application/octet-stream");
        form.Add(file, "file", fileName);
        if (name is not null) form.Add(new StringContent(name), "name");
        form.Add(new StringContent(shared ? "true" : "false"), "sharedWithStudents");
        return form;
    }

    /// <summary>Tạo lộ trình nháp có 1 bài, 1 chapter → (id lộ trình, id bài, id chapter)</summary>
    private async Task<(Guid Id, Guid Lesson, Guid Chapter)> DraftWithChapter(HttpClient c, string? code = null)
    {
        var cur = await Ok(await c.PostAsJsonAsync(Curricula, NewCurriculum(code ?? NewCode("LT")), Ct));
        var id = cur.GetProperty("id").GetGuid();
        var withLesson = await Ok(await c.PostAsJsonAsync($"{Curricula}/{id}/lessons", new { title = "Bài 1", hasQuiz = false }, Ct));
        var lesson = withLesson.GetProperty("lessons")[0].GetProperty("id").GetGuid();
        var withChapter = await Ok(await c.PostAsJsonAsync($"{Curricula}/{id}/lessons/{lesson}/chapters", new { title = "Từ vựng" }, Ct));
        return (id, lesson, withChapter.GetProperty("lessons")[0].GetProperty("chapters")[0].GetProperty("id").GetGuid());
    }

    // ================= Khóa học =================

    [Fact]
    public async Task KH01_Danh_sach_khoa_tu_du_lieu_mau()
    {
        var c = await SignIn();
        var list = await Ok(await c.GetAsync(Courses, Ct));
        list.GetArrayLength().ShouldBeGreaterThanOrEqualTo(14);

        var n5 = list.EnumerateArray().Single(x => x.GetProperty("id").GetGuid() == N5Course);
        n5.GetProperty("code").GetString().ShouldBe("N5-G-OFF");
        n5.GetProperty("levelFrom").GetString().ShouldBe("beginner");
        n5.GetProperty("levelTo").GetString().ShouldBe("N5");
        n5.GetProperty("status").GetString().ShouldBe("active");
        n5.GetProperty("fee").GetDecimal().ShouldBe(4_900_000);
        var def = n5.GetProperty("curricula").EnumerateArray().Single(x => x.GetProperty("isDefault").GetBoolean());
        def.GetProperty("curriculumId").GetGuid().ShouldBe(N5Curriculum);
    }

    [Fact]
    public async Task KH01_Pham_vi_chi_nhanh_va_khong_co_quyen()
    {
        // Giáo vụ: phạm vi chi nhánh CG, HC → không thấy khóa chỉ bán ở Q1 / online khác chi nhánh
        var gv = await SignIn("giaovu@jpcenter.vn");
        var list = await Ok(await gv.GetAsync(Courses, Ct));
        var allowed = new[] { Cg, SeedData.IdOf("hc") };
        list.EnumerateArray().ShouldAllBe(x => x.GetProperty("branchIds").EnumerateArray().Any(b => allowed.Contains(b.GetGuid())));

        var kt = await SignIn("ketoan@jpcenter.vn");
        (await kt.GetAsync(Courses, Ct)).StatusCode.ShouldBe(HttpStatusCode.Forbidden);
        (await kt.PostAsJsonAsync(Courses, NewCourse(NewCode("X")), Ct)).StatusCode.ShouldBe(HttpStatusCode.Forbidden);

        // Ô chọn khóa: mọi nhân viên
        var options = await Ok(await kt.GetAsync($"{Courses}/options?branchId={Cg}", Ct));
        options.EnumerateArray().ShouldAllBe(x => x.GetProperty("branchIds").EnumerateArray().Any(b => b.GetGuid() == Cg));
    }

    [Fact]
    public async Task KH02_KH05_Them_khoa_trung_ma_mo_tuyen_sinh_can_lo_trinh()
    {
        var c = await SignIn();
        var code = NewCode("kh");
        var created = await c.PostAsJsonAsync(Courses, NewCourse(code), Ct);
        created.StatusCode.ShouldBe(HttpStatusCode.Created);
        var course = await Json(created);
        var id = course.GetProperty("id").GetGuid();
        course.GetProperty("code").GetString().ShouldBe(code.ToUpperInvariant());
        course.GetProperty("status").GetString().ShouldBe("draft");

        var dup = await c.PostAsJsonAsync(Courses, NewCourse(code.ToLowerInvariant()), Ct);
        dup.StatusCode.ShouldBe(HttpStatusCode.Conflict);
        (await Code(dup)).ShouldBe("academic.course.duplicate_code");

        var noCurriculum = await c.PostAsync($"{Courses}/{id}/activate", null, Ct);
        (await Code(noCurriculum)).ShouldBe("academic.course.no_default_curriculum");

        // Bản nháp không đặt mặc định được (E4) — gắn thường thì tự thành mặc định vì là lộ trình đầu tiên
        (await Code(await c.PostAsJsonAsync($"{Courses}/{id}/curricula", new { curriculumId = N3Draft, isDefault = true }, Ct)))
            .ShouldBe("academic.course.default_not_published");
        var linked = await Ok(await c.PostAsJsonAsync($"{Courses}/{id}/curricula", new { curriculumId = N5Curriculum, isDefault = true }, Ct));
        linked.GetProperty("curricula")[0].GetProperty("isDefault").GetBoolean().ShouldBeTrue();

        (await Ok(await c.PostAsync($"{Courses}/{id}/activate", null, Ct))).GetProperty("status").GetString().ShouldBe("active");
        (await Ok(await c.PostAsync($"{Courses}/{id}/pause", null, Ct))).GetProperty("status").GetString().ShouldBe("paused");

        // Gỡ lộ trình mặc định → chặn (KH-06.E1)
        (await Code(await c.DeleteAsync($"{Courses}/{id}/curricula/{N5Curriculum}", Ct))).ShouldBe("academic.course.default_curriculum");
    }

    [Fact]
    public async Task KH02_Quy_tac_va_validate()
    {
        var c = await SignIn();
        (await Code(await c.PostAsJsonAsync(Courses, NewCourse(NewCode("KH"), maxStudents: null), Ct))).ShouldBe("academic.course.max_students_required");
        var r = await c.PostAsJsonAsync(Courses, new { code = "", name = "", category = "jlpt", levelFrom = "N5", levelTo = "N4", format = "group",
            teacherType = "vn", mode = "offline", sessions = 0, minutesPerSession = 90, sessionsPerWeek = 2, fee = 0, maxStudents = 10,
            branchIds = Array.Empty<Guid>() }, Ct);
        r.StatusCode.ShouldBe(HttpStatusCode.BadRequest);
    }

    [Fact]
    public async Task KH03_KH04_Nhan_ban_giu_lo_trinh_va_sua_de_bi_chan()
    {
        var c = await SignIn();
        var clone = await Ok(await c.PostAsJsonAsync(Courses, NewCourse(NewCode("CL"), cloneFromId: N5Course), Ct));
        var curricula = clone.GetProperty("curricula").EnumerateArray().ToList();
        curricula.ShouldContain(x => x.GetProperty("curriculumId").GetGuid() == N5Curriculum && x.GetProperty("isDefault").GetBoolean());
        curricula.ShouldNotContain(x => x.GetProperty("curriculumId").GetGuid() == SeedData.IdOf("CUR-N5-V1")); // bản lưu trữ không chép

        var id = clone.GetProperty("id").GetGuid();
        var version = clone.GetProperty("rowVersion").GetUInt32();
        var code = clone.GetProperty("code").GetString()!;
        (await Ok(await c.PutAsJsonAsync($"{Courses}/{id}", NewCourse(code, rowVersion: version, fee: 6_000_000), Ct))).GetProperty("fee").GetDecimal()
            .ShouldBe(6_000_000);
        var stale = await c.PutAsJsonAsync($"{Courses}/{id}", NewCourse(code, rowVersion: version, fee: 7_000_000), Ct);
        stale.StatusCode.ShouldBe(HttpStatusCode.Conflict);
        (await Code(stale)).ShouldBe("system.concurrency");
    }

    // ================= Lộ trình =================

    [Fact]
    public async Task LT01_Danh_sach_lo_trinh_kem_noi_dung()
    {
        var c = await SignIn();
        var list = await Ok(await c.GetAsync(Curricula, Ct));
        var n5 = list.EnumerateArray().Single(x => x.GetProperty("id").GetGuid() == N5Curriculum);
        n5.GetProperty("version").GetString().ShouldBe("v2.0");
        n5.GetProperty("status").GetString().ShouldBe("published");
        n5.GetProperty("level").GetString().ShouldBe("N5");
        n5.GetProperty("author").GetString().ShouldBe("Trần Mai Anh");
        n5.GetProperty("lessons").GetArrayLength().ShouldBe(16);
        n5.GetProperty("courses").EnumerateArray().ShouldContain(x => x.GetProperty("courseId").GetGuid() == N5Course);

        var materials = n5.GetProperty("lessons").EnumerateArray().SelectMany(l => l.GetProperty("chapters").EnumerateArray())
            .SelectMany(ch => ch.GetProperty("materials").EnumerateArray()).ToList();
        var file = materials.First(m => m.GetProperty("isFile").GetBoolean());
        file.TryGetProperty("url", out _).ShouldBeFalse(); // file chỉ mở qua link tạm (JSON bỏ trường null)
        var link = await Ok(await c.GetAsync($"{Curricula}/{N5Curriculum}/materials/{file.GetProperty("id").GetGuid()}/link?download=true", Ct));
        link.GetProperty("url").GetString()!.ShouldStartWith("https://files.test/academic/materials/demo/");
        link.GetProperty("url").GetString()!.ShouldContain("inline=False");
    }

    [Fact]
    public async Task LT02_LT05_LT06_LT09_Soan_tai_lieu_phat_hanh()
    {
        var c = await SignIn();
        var (id, lesson, chapter) = await DraftWithChapter(c);
        var chapters = $"{Curricula}/{id}/lessons/{lesson}/chapters/{chapter}";

        // Tải PDF (2 MB — vượt giới hạn 1 MB chung của API, endpoint tải file nới riêng)
        var big = new byte[2 * 1024 * 1024];
        Pdf.CopyTo(big, 0);
        var uploaded = await Ok(await c.PostAsync($"{chapters}/materials/file", FileForm(big, "Bài đọc 1.pdf", shared: false), Ct));
        var m = uploaded.GetProperty("lessons")[0].GetProperty("chapters")[0].GetProperty("materials")[0];
        m.GetProperty("name").GetString().ShouldBe("Bài đọc 1");
        m.GetProperty("type").GetString().ShouldBe("pdf");
        m.GetProperty("isFile").GetBoolean().ShouldBeTrue();
        m.GetProperty("sharedWithStudents").GetBoolean().ShouldBeFalse();
        m.GetProperty("size").GetInt64().ShouldBe(big.Length);

        // Đổi đuôi để lách → chặn; đuôi không nhận → chặn
        (await Code(await c.PostAsync($"{chapters}/materials/file", FileForm([0x4D, 0x5A, 0x90, 0x00], "de.pdf"), Ct))).ShouldBe("file.content_mismatch");
        (await Code(await c.PostAsync($"{chapters}/materials/file", FileForm(Pdf, "video.mp4"), Ct))).ShouldBe("file.type_not_allowed");

        // Link: video YouTube; Drive sai miền → chặn
        await Ok(await c.PostAsJsonAsync($"{chapters}/materials/link",
            new { name = "Video ngữ pháp", type = "video", url = "https://youtu.be/abc", sharedWithStudents = true }, Ct));
        (await Code(await c.PostAsJsonAsync($"{chapters}/materials/link",
            new { name = "Drive", type = "drive", url = "https://dropbox.com/x", sharedWithStudents = true }, Ct))).ShouldBe("academic.material.not_drive");

        // Mốc thi + phát hành → khóa nội dung
        await Ok(await c.PostAsJsonAsync($"{Curricula}/{id}/exams", new { kind = "final", title = "Cuối kỳ", afterLessonId = lesson, note = (string?)null }, Ct));
        var published = await Ok(await c.PostAsJsonAsync($"{Curricula}/{id}/publish", new { setDefaultFor = (Guid[]?)null }, Ct));
        published.GetProperty("status").GetString().ShouldBe("published");
        var locked = await c.PostAsJsonAsync($"{Curricula}/{id}/lessons", new { title = "Bài 2", hasQuiz = false }, Ct);
        locked.StatusCode.ShouldBe(HttpStatusCode.Conflict);
        (await Code(locked)).ShouldBe("academic.curriculum.not_draft");
    }

    [Fact]
    public async Task LT10_Phien_ban_moi_dung_chung_file_xoa_dung_luc()
    {
        var c = await SignIn();
        var (id, lesson, chapter) = await DraftWithChapter(c);
        var withFile = await Ok(await c.PostAsync($"{Curricula}/{id}/lessons/{lesson}/chapters/{chapter}/materials/file", FileForm(Pdf, "a.pdf"), Ct));
        var key = await KeyOf(withFile.GetProperty("lessons")[0].GetProperty("chapters")[0].GetProperty("materials")[0].GetProperty("id").GetGuid());
        await Ok(await c.PostAsync($"{Curricula}/{id}/publish", JsonContent.Create(new { }), Ct));

        var draft = await Json(await c.PostAsync($"{Curricula}/{id}/new-version", null, Ct));
        draft.GetProperty("version").GetString().ShouldBe("v1.1");
        draft.GetProperty("previousVersionId").GetGuid().ShouldBe(id);
        (await Code(await c.PostAsync($"{Curricula}/{id}/new-version", null, Ct))).ShouldBe("academic.curriculum.draft_exists");

        // Xóa tài liệu khỏi bản nháp: bản đã phát hành còn dùng → file giữ nguyên
        var draftId = draft.GetProperty("id").GetGuid();
        var copied = draft.GetProperty("lessons")[0].GetProperty("chapters")[0].GetProperty("materials")[0].GetProperty("id").GetGuid();
        (await KeyOf(copied)).ShouldBe(key);
        await Ok(await c.DeleteAsync($"{Curricula}/{draftId}/materials/{copied}", Ct));
        api.Files.Files.ContainsKey(key).ShouldBeTrue();

        // Tài liệu chỉ có ở bản nháp: xóa → file dọn khỏi kho
        var draftLesson = draft.GetProperty("lessons")[0];
        var only = await Ok(await c.PostAsync(
            $"{Curricula}/{draftId}/lessons/{draftLesson.GetProperty("id").GetGuid()}/chapters/{draftLesson.GetProperty("chapters")[0].GetProperty("id").GetGuid()}/materials/file",
            FileForm(Pdf, "rieng.pdf"), Ct));
        var onlyId = only.GetProperty("lessons")[0].GetProperty("chapters")[0].GetProperty("materials")[0].GetProperty("id").GetGuid();
        var onlyKey = await KeyOf(onlyId);
        api.Files.Files.ContainsKey(onlyKey).ShouldBeTrue();
        await Ok(await c.DeleteAsync($"{Curricula}/{draftId}/materials/{onlyId}", Ct));
        api.Files.Files.ContainsKey(onlyKey).ShouldBeFalse();
    }

    [Fact]
    public async Task LT03_Nhan_ban_ban_nang_cao_tu_gan_vao_khoa()
    {
        var c = await SignIn();
        (await Code(await c.PostAsJsonAsync(Curricula, NewCurriculum(NewCode("ADV"), "advanced", cloneFromId: N5Curriculum), Ct)))
            .ShouldBe("academic.curriculum.entry_requirement_required");
        var adv = await Ok(await c.PostAsJsonAsync(Curricula,
            NewCurriculum(NewCode("ADV"), "advanced", "Điểm test ≥ 80%", N5Curriculum), Ct));
        adv.GetProperty("status").GetString().ShouldBe("draft");
        adv.GetProperty("lessons").GetArrayLength().ShouldBe(16);
        adv.GetProperty("courses").EnumerateArray().ShouldContain(x => x.GetProperty("courseId").GetGuid() == N5Course && !x.GetProperty("isDefault").GetBoolean());
    }

    [Fact]
    public async Task LT11_Luu_tru_ban_mac_dinh_bi_chan_xoa_nhap_giu_noi_dung()
    {
        var c = await SignIn();
        (await Code(await c.PostAsync($"{Curricula}/{N5Curriculum}/archive", null, Ct))).ShouldBe("academic.curriculum.is_default");

        var (id, _, _) = await DraftWithChapter(c);
        (await c.DeleteAsync($"{Curricula}/{id}", Ct)).StatusCode.ShouldBe(HttpStatusCode.NoContent);
        (await c.GetAsync($"{Curricula}/{id}", Ct)).StatusCode.ShouldBe(HttpStatusCode.NotFound);

        // Xóa mềm: bài học / chapter vẫn còn để khôi phục từ Thùng rác
        await using var scope = api.Scope();
        var db = scope.ServiceProvider.GetRequiredService<AcademicDbContext>();
        var deleted = await db.Curricula.IgnoreQueryFilters().AsSplitQuery().SingleAsync(x => x.Id == new CurriculumId(id), Ct);
        deleted.Lessons.Single().Chapters.Single().Title.ShouldBe("Từ vựng");
    }

    [Fact]
    public async Task LT05_Xoa_bai_doi_thu_tu()
    {
        var c = await SignIn();
        var (id, first, _) = await DraftWithChapter(c);
        var two = await Ok(await c.PostAsJsonAsync($"{Curricula}/{id}/lessons", new { title = "Bài 2", hasQuiz = true }, Ct));
        var second = two.GetProperty("lessons")[1].GetProperty("id").GetGuid();
        await Ok(await c.PostAsJsonAsync($"{Curricula}/{id}/exams", new { kind = "midterm", title = "Giữa kỳ", afterLessonId = second, note = "x" }, Ct));

        var reordered = await Ok(await c.PutAsJsonAsync($"{Curricula}/{id}/lessons/order", new { ids = new[] { second, first } }, Ct));
        reordered.GetProperty("lessons")[0].GetProperty("title").GetString().ShouldBe("Bài 2");
        (await Code(await c.PutAsJsonAsync($"{Curricula}/{id}/lessons/order", new { ids = new[] { second } }, Ct)))
            .ShouldBe("academic.curriculum.invalid_order");

        var removed = await Ok(await c.DeleteAsync($"{Curricula}/{id}/lessons/{second}", Ct));
        removed.GetProperty("lessons").GetArrayLength().ShouldBe(1);
        removed.GetProperty("exams")[0].GetProperty("afterLessonId").GetGuid().ShouldBe(first);
    }

    private async Task<string> KeyOf(Guid materialId)
    {
        await using var scope = api.Scope();
        var db = scope.ServiceProvider.GetRequiredService<AcademicDbContext>();
        var all = await db.Curricula.IgnoreQueryFilters().AsNoTracking().AsSplitQuery().ToListAsync(Ct);
        return all.SelectMany(x => x.AllMaterials).Single(m => m.Id == new MaterialId(materialId)).FileKey!;
    }
}
