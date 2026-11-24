using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text.Json;
using JPCenter.Infrastructure.Common.Persistence;
using JPCenter.IntegrationTests.Infrastructure;
using JPCenter.Modules.System.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;

namespace JPCenter.IntegrationTests.Library;

/// <summary>User story 16 (giai đoạn 23 phase 3) — thư viện điện tử: thêm / sửa / ẩn / xóa, lượt xem có watermark, thống kê, cổng HV / PH / GV.</summary>
[Collection(SeededCollection.Name)]
public sealed class LibraryFlowTests(SeededApiFactory api)
{
    private const string Docs = "/api/v1/library/documents";
    private const string Learning = "/api/v1/library/portal/learning/library";
    private const string Teaching = "/api/v1/library/portal/teaching/library";
    private const string Admin = "admin@jpcenter.vn", GiaoVu = "giaovu@jpcenter.vn", QuanLyCg = "quanly.caugiay@jpcenter.vn", Sale = "tung.tung@jpcenter.vn",
        Teacher = "giaovien@jpcenter.vn", StudentAcc = "hocvien@jpcenter.vn", ParentAcc = "phuhuynh@jpcenter.vn";
    private static CancellationToken Ct => TestContext.Current.CancellationToken;

    private static readonly Guid Cg = SeedData.IdOf("cg");
    private static readonly Guid Hc = SeedData.IdOf("hc");
    private static readonly Guid St1 = SeedData.IdOf("ST-1");
    private static readonly Guid DocN3Exam = SeedData.IdOf("DOC-3");
    private static readonly Guid DocHcOnly = SeedData.IdOf("DOC-10");
    private static readonly Guid DocNhkLink = SeedData.IdOf("DOC-12");
    private static readonly Guid DocTeacherOnly = SeedData.IdOf("DOC-13");
    private static readonly Guid DocHidden = SeedData.IdOf("DOC-14");

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
        return body.Length == 0 ? default : JsonDocument.Parse(body).RootElement;
    }

    private static async Task Expect(HttpResponseMessage r, HttpStatusCode status, string? code = null)
    {
        var body = await r.Content.ReadAsStringAsync(Ct);
        r.StatusCode.ShouldBe(status, body);
        if (code is not null) JsonDocument.Parse(body).RootElement.GetProperty("code").GetString().ShouldBe(code);
    }

    /// <summary>API bỏ trường null khỏi JSON</summary>
    private static bool Missing(JsonElement e, string name) => !e.TryGetProperty(name, out var v) || v.ValueKind == JsonValueKind.Null;

    private static List<Guid> Ids(JsonElement list) => list.EnumerateArray().Select(x => x.GetProperty("id").GetGuid()).ToList();

    private static async Task<JsonElement> Upload(HttpClient c, string fileName = "de-thi.pdf")
    {
        using var form = new MultipartFormDataContent();
        var content = new ByteArrayContent([.. "%PDF-1.4 thu vien "u8.ToArray(), .. new byte[200]]);
        content.Headers.ContentType = new MediaTypeHeaderValue("application/pdf");
        form.Add(content, "file", fileName);
        return await Ok(await c.PostAsync($"{Docs}/files", form, Ct));
    }

    private static object Body(JsonElement file, string title, Guid[]? branchIds = null, string[]? audience = null, uint? rowVersion = null) => new
    {
        title, subtitle = "日本語能力試験", category = "jlpt", fileType = "pdf", fileKey = file.GetProperty("fileKey").GetString(),
        fileName = file.GetProperty("fileName").GetString(), size = file.GetProperty("size").GetInt64(), levels = new[] { "N3" },
        skills = new[] { "reading" }, tags = new[] { " Đề thi ", "đề thi", "N3" }, pages = 30, year = 2025, publisher = "JEES",
        audience = audience ?? ["student", "teacher"], branchIds = branchIds ?? [], rowVersion,
    };

    private static async Task Eventually(Func<Task<bool>> condition)
    {
        for (var i = 0; i < 40; i++)
        {
            if (await condition()) return;
            await Task.Delay(250, Ct);
        }
        (await condition()).ShouldBeTrue("Hết thời gian chờ việc chạy nền");
    }

    [Fact(DisplayName = "TV-01: lọc + phạm vi chi nhánh; tài liệu ẩn chỉ hiện khi yêu cầu; Sale không có quyền")]
    public async Task List_filter_and_scope()
    {
        var admin = await SignIn(Admin);
        var all = await Ok(await admin.GetAsync(Docs, Ct));
        Ids(all).ShouldNotContain(DocHidden);
        Ids(await Ok(await admin.GetAsync($"{Docs}?includeHidden=true", Ct))).ShouldContain(DocHidden);
        var doc3 = all.EnumerateArray().Single(d => d.GetProperty("id").GetGuid() == DocN3Exam);
        doc3.GetProperty("watermark").GetBoolean().ShouldBeTrue();
        doc3.GetProperty("allowDownload").GetBoolean().ShouldBeFalse();
        doc3.GetProperty("stats").GetProperty("daily").GetArrayLength().ShouldBe(14);
        doc3.GetProperty("stats").GetProperty("views").GetInt32().ShouldBeGreaterThan(0);
        doc3.GetProperty("levels")[0].GetString().ShouldBe("N3");

        var n3 = await Ok(await admin.GetAsync($"{Docs}?level=N3&fileType=pdf", Ct));
        n3.EnumerateArray().ShouldAllBe(d => d.GetProperty("fileType").GetString() == "pdf");
        Ids(n3).ShouldContain(DocN3Exam);
        Ids(await Ok(await admin.GetAsync($"{Docs}?q=de thi jlpt n3", Ct))).ShouldContain(DocN3Exam);   // không dấu
        Ids(await Ok(await admin.GetAsync($"{Docs}?audience=teacher&category=textbook", Ct))).ShouldNotContain(DocNhkLink);

        // Quản lý Cầu Giấy (phạm vi branch): không thấy tài liệu chỉ của Hải Châu; giáo vụ (Cầu Giấy + Hải Châu) thấy
        Ids(await Ok(await (await SignIn(QuanLyCg)).GetAsync(Docs, Ct))).ShouldNotContain(DocHcOnly);
        Ids(await Ok(await (await SignIn(GiaoVu)).GetAsync(Docs, Ct))).ShouldContain(DocHcOnly);
        await Expect(await (await SignIn(QuanLyCg)).PostAsync($"{Docs}/{DocHcOnly}/views", null, Ct), HttpStatusCode.NotFound, "library.document.not_found");

        await Expect(await (await SignIn(Sale)).GetAsync(Docs, Ct), HttpStatusCode.Forbidden);
    }

    [Fact(DisplayName = "TV-02 → 04: tải tệp, thêm (mặc định watermark + chặn tải), lỗi nhập, sửa đè 409, ẩn / hiện, cho tải ghi nhật ký")]
    public async Task Create_update_protect()
    {
        var gv = await SignIn(GiaoVu);
        var file = await Upload(gv);
        file.GetProperty("fileKey").GetString()!.ShouldStartWith("library/documents/");
        file.GetProperty("fileType").GetString().ShouldBe("pdf");

        // Giáo vụ (phạm vi branch) không tạo được tài liệu dùng chung mọi chi nhánh
        await Expect(await gv.PostAsJsonAsync(Docs, Body(file, "Đề N3 thử"), Ct), HttpStatusCode.Forbidden, "library.branch_not_allowed");
        await Expect(await gv.PostAsJsonAsync(Docs, Body(file, "Đề N3 thử", [Cg], audience: []), Ct), HttpStatusCode.BadRequest,
            "library.document.audience_required");
        await Expect(await gv.PostAsJsonAsync(Docs, Body(file, " ", [Cg]), Ct), HttpStatusCode.BadRequest, "library.document.title_required");
        await Expect(await gv.PostAsJsonAsync(Docs, new { title = "Link sai", category = "media", fileType = "link", fileKey = "ftp://x", audience = new[] { "student" }, branchIds = new[] { Cg } }, Ct),
            HttpStatusCode.BadRequest, "library.document.invalid_link");
        // Khóa tệp không do hệ thống cấp
        await Expect(await gv.PostAsJsonAsync(Docs, new { title = "Tệp lạ", category = "jlpt", fileType = "pdf", fileKey = "academic/materials/x.pdf", fileName = "x.pdf", audience = new[] { "student" }, branchIds = new[] { Cg } }, Ct),
            HttpStatusCode.BadRequest, "library.document.file_required");

        var created = await Ok(await gv.PostAsJsonAsync(Docs, Body(file, "Đề N3 thử nghiệm", [Cg]), Ct));
        var id = created.GetProperty("id").GetGuid();
        (created.GetProperty("watermark").GetBoolean(), created.GetProperty("allowDownload").GetBoolean(), created.GetProperty("status").GetString())
            .ShouldBe((true, false, "published"));
        created.GetProperty("tags").EnumerateArray().Select(t => t.GetString()).ShouldBe(["Đề thi", "N3"]);
        created.GetProperty("uploadedBy").GetString().ShouldNotBe("—");

        // Giáo vụ không có quyền sửa (library.documents.update)
        await Expect(await gv.PutAsJsonAsync($"{Docs}/{id}", Body(file, "Sửa", [Cg], rowVersion: created.GetProperty("rowVersion").GetUInt32()), Ct),
            HttpStatusCode.Forbidden);

        var admin = await SignIn(Admin);
        var v1 = created.GetProperty("rowVersion").GetUInt32();
        var updated = await Ok(await admin.PutAsJsonAsync($"{Docs}/{id}", Body(file, "Đề N3 (bản 2)", [Cg], rowVersion: v1), Ct));
        updated.GetProperty("title").GetString().ShouldBe("Đề N3 (bản 2)");
        await Expect(await admin.PutAsJsonAsync($"{Docs}/{id}", Body(file, "Sửa đè", [Cg], rowVersion: v1), Ct), HttpStatusCode.Conflict);
        // Không gửi khóa tệp (form sửa không tải lại tệp) → giữ tệp hiện tại
        updated = await Ok(await admin.PutAsJsonAsync($"{Docs}/{id}",
            new { title = "Đề N3 (bản 3)", category = "jlpt", fileType = "pdf", levels = new[] { "n3" }, rowVersion = updated.GetProperty("rowVersion").GetUInt32() }, Ct));
        (updated.GetProperty("title").GetString(), updated.GetProperty("fileName").GetString()).ShouldBe(("Đề N3 (bản 3)", file.GetProperty("fileName").GetString()));

        // TV-04
        await Expect(await admin.PutAsJsonAsync($"{Docs}/{id}/access", new { audience = Array.Empty<string>(), branchIds = new[] { Cg }, watermark = true, allowDownload = false, rowVersion = updated.GetProperty("rowVersion").GetUInt32() }, Ct),
            HttpStatusCode.BadRequest, "library.document.audience_required");
        var opened = await Ok(await admin.PutAsJsonAsync($"{Docs}/{id}/access",
            new { audience = new[] { "student" }, branchIds = new[] { Cg, Hc }, watermark = true, allowDownload = true, rowVersion = updated.GetProperty("rowVersion").GetUInt32() }, Ct));
        (opened.GetProperty("allowDownload").GetBoolean(), opened.GetProperty("branchIds").GetArrayLength()).ShouldBe((true, 2));
        await Eventually(async () =>
        {
            await using var scope = api.Scope();
            return await scope.ServiceProvider.GetRequiredService<SystemDbContext>().AuditLogs.AsNoTracking()
                .AnyAsync(l => l.Action == "Cho tải về tài liệu thư viện" && l.Target == "Đề N3 (bản 3)", Ct);
        });

        // TV-03 ẩn / hiện (ẩn 2 lần không lỗi)
        (await Ok(await admin.PostAsync($"{Docs}/{id}/hide", null, Ct))).GetProperty("status").GetString().ShouldBe("hidden");
        (await Ok(await admin.PostAsync($"{Docs}/{id}/hide", null, Ct))).GetProperty("status").GetString().ShouldBe("hidden");
        Ids(await Ok(await admin.GetAsync(Docs, Ct))).ShouldNotContain(id);
        (await Ok(await admin.PostAsync($"{Docs}/{id}/publish", null, Ct))).GetProperty("status").GetString().ShouldBe("published");
    }

    [Fact(DisplayName = "TV-05, TV-06: lượt xem — link inline 10 phút + watermark, tải về chỉ khi cho phép, kết thúc lượt, thống kê")]
    public async Task Views_and_stats()
    {
        var admin = await SignIn(Admin);
        var before = await Ok(await admin.GetAsync($"{Docs}/{DocN3Exam}/stats", Ct));

        var view = await Ok(await admin.PostAsync($"{Docs}/{DocN3Exam}/views", null, Ct));
        view.GetProperty("url").GetString()!.ShouldContain("inline=True");
        Missing(view, "downloadUrl").ShouldBeTrue();
        view.GetProperty("allowDownload").GetBoolean().ShouldBeFalse();
        var mark = view.GetProperty("watermark").GetString()!;
        mark.ShouldContain(" · ");
        mark.ShouldContain("@jpcenter.vn");
        view.GetProperty("rowVersion").GetUInt32().ShouldBeGreaterThan(0u);
        var viewId = view.GetProperty("viewId").GetGuid();

        // Người khác không kết thúc được lượt của mình (E3); kết thúc 2 lần không lỗi
        await Expect(await (await SignIn(GiaoVu)).PostAsync($"/api/v1/library/views/{viewId}/end", null, Ct), HttpStatusCode.NotFound,
            "library.view.not_found");
        await Ok(await admin.PostAsync($"/api/v1/library/views/{viewId}/end", null, Ct));
        await Ok(await admin.PostAsync($"/api/v1/library/views/{viewId}/end", null, Ct));

        var after = await Ok(await admin.GetAsync($"{Docs}/{DocN3Exam}/stats", Ct));
        after.GetProperty("views").GetInt32().ShouldBe(before.GetProperty("views").GetInt32() + 1);
        after.GetProperty("daily")[13].GetInt32().ShouldBe(before.GetProperty("daily")[13].GetInt32() + 1);
        after.GetProperty("top").GetArrayLength().ShouldBeLessThanOrEqualTo(6);
        var listed = (await Ok(await admin.GetAsync(Docs, Ct))).EnumerateArray().Single(d => d.GetProperty("id").GetGuid() == DocN3Exam);
        listed.GetProperty("stats").GetProperty("views").GetInt32().ShouldBe(after.GetProperty("views").GetInt32());

        // Cho tải (DOC-6) → có link tải; link ngoài (DOC-12) → không watermark
        var download = await Ok(await admin.PostAsync($"{Docs}/{SeedData.IdOf("DOC-6")}/views", null, Ct));
        download.GetProperty("downloadUrl").GetString()!.ShouldContain("inline=False");
        var link = await Ok(await admin.PostAsync($"{Docs}/{DocNhkLink}/views", null, Ct));
        link.GetProperty("url").GetString().ShouldBe("https://www3.nhk.or.jp/news/easy/");
        Missing(link, "watermark").ShouldBeTrue();

        await Expect(await (await SignIn(Sale)).PostAsync($"{Docs}/{DocN3Exam}/views", null, Ct), HttpStatusCode.Forbidden);
    }

    [Fact(DisplayName = "TV-07, TV-08: cổng HV / PH / GV — đúng đối tượng + chi nhánh, đúng cấp độ lên trước, không lộ số liệu; lượt xem theo loại người xem")]
    public async Task Portals()
    {
        var student = await SignIn(StudentAcc);
        var list = await Ok(await student.GetAsync(Learning, Ct));
        var ids = Ids(list);
        ids.ShouldContain(DocN3Exam);
        ids.ShouldNotContain(DocTeacherOnly);
        ids.ShouldNotContain(DocHidden);
        list[0].GetProperty("matchesLevel").GetBoolean().ShouldBeTrue();
        list[0].TryGetProperty("stats", out _).ShouldBeFalse();
        list[0].TryGetProperty("audience", out _).ShouldBeFalse();

        await Expect(await student.PostAsync($"{Learning}/{DocTeacherOnly}/views", null, Ct), HttpStatusCode.NotFound, "library.document.not_found");
        await Expect(await student.PostAsync($"{Learning}/{DocHidden}/views", null, Ct), HttpStatusCode.NotFound, "library.document.not_found");
        var view = await Ok(await student.PostAsync($"{Learning}/{DocN3Exam}/views", null, Ct));
        view.GetProperty("watermark").GetString()!.ShouldContain("HV1000");
        Missing(view, "rowVersion").ShouldBeTrue();
        await Ok(await student.PostAsync($"{Learning}/views/{view.GetProperty("viewId").GetGuid()}/end", null, Ct));

        // PH nhiều con phải chọn con; lượt xem ghi là phụ huynh
        var parent = await SignIn(ParentAcc);
        await Expect(await parent.GetAsync(Learning, Ct), HttpStatusCode.NotFound);
        Ids(await Ok(await parent.GetAsync($"{Learning}?studentId={St1}", Ct))).ShouldContain(DocN3Exam);
        await Ok(await parent.PostAsync($"{Learning}/{DocN3Exam}/views?studentId={St1}", null, Ct));
        var stats = await Ok(await (await SignIn(Admin)).GetAsync($"{Docs}/{DocN3Exam}/stats", Ct));
        stats.GetProperty("top").EnumerateArray().ShouldContain(t => t.GetProperty("role").GetString() == "Phụ huynh");

        // GV: tài liệu cho GV, không thấy tài liệu chỉ cho HV
        var teacher = await SignIn(Teacher);
        var mine = Ids(await Ok(await teacher.GetAsync(Teaching, Ct)));
        mine.ShouldContain(DocTeacherOnly);
        mine.ShouldNotContain(DocNhkLink);
        var tv = await Ok(await teacher.PostAsync($"{Teaching}/{DocTeacherOnly}/views", null, Ct));
        await Ok(await teacher.PostAsync($"{Teaching}/views/{tv.GetProperty("viewId").GetGuid()}/end", null, Ct));

        // Sai cổng
        await Expect(await student.GetAsync(Teaching, Ct), HttpStatusCode.Forbidden);
        await Expect(await teacher.GetAsync(Learning, Ct), HttpStatusCode.Forbidden);
    }

    [Fact(DisplayName = "TV-03: xóa → Thùng rác, khôi phục giữ số liệu; xóa hẳn xóa cả tệp trong kho")]
    public async Task Delete_restore_purge()
    {
        var admin = await SignIn(Admin);
        var file = await Upload(admin, "xoa.pdf");
        var key = file.GetProperty("fileKey").GetString()!;
        var id = (await Ok(await admin.PostAsJsonAsync(Docs, Body(file, "Tài liệu sẽ xóa"), Ct))).GetProperty("id").GetGuid();
        await Ok(await admin.PostAsync($"{Docs}/{id}/views", null, Ct));

        await Expect(await (await SignIn(GiaoVu)).DeleteAsync($"{Docs}/{id}", Ct), HttpStatusCode.Forbidden);
        await Ok(await admin.DeleteAsync($"{Docs}/{id}", Ct));
        Ids(await Ok(await admin.GetAsync($"{Docs}?includeHidden=true", Ct))).ShouldNotContain(id);

        async Task<Guid?> TrashId()
        {
            await using var scope = api.Scope();
            return await scope.ServiceProvider.GetRequiredService<SystemDbContext>().TrashItems.AsNoTracking()
                .Where(t => t.Kind == "library.document" && t.EntityId == id).Select(t => (Guid?)t.Id.Value).FirstOrDefaultAsync(Ct);
        }
        await Eventually(async () => await TrashId() is not null);
        await Ok(await admin.PostAsync($"/api/v1/system/trash/{await TrashId()}/restore", null, Ct));
        var back = (await Ok(await admin.GetAsync(Docs, Ct))).EnumerateArray().Single(d => d.GetProperty("id").GetGuid() == id);
        back.GetProperty("stats").GetProperty("views").GetInt32().ShouldBe(1);

        await Ok(await admin.DeleteAsync($"{Docs}/{id}", Ct));
        await Eventually(async () => await TrashId() is not null);
        api.Files.Files.ContainsKey(key).ShouldBeTrue();
        await Ok(await admin.DeleteAsync($"/api/v1/system/trash/{await TrashId()}", Ct));
        api.Files.Files.ContainsKey(key).ShouldBeFalse();
    }
}
