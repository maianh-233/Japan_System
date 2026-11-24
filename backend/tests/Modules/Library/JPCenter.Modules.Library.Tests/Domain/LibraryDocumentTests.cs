using JPCenter.Modules.Library.Domain.Documents;
using JPCenter.SharedKernel.Primitives;

namespace JPCenter.Modules.Library.Tests.Domain;

/// <summary>User story 16 — thư viện tài liệu điện tử</summary>
public sealed class LibraryDocumentTests
{
    private static readonly DateTimeOffset Now = new(2026, 11, 24, 3, 0, 0, TimeSpan.Zero);
    private static readonly TimeSpan Vn = TimeSpan.FromHours(7);
    private static readonly Guid Staff = Guid.NewGuid();
    private static readonly BranchId HaNoi = new(Guid.NewGuid()), Hcm = new(Guid.NewGuid());

    private static LibraryDocumentInfo Pdf(string? title = "Minna no Nihongo I", string? key = "library/documents/minna1.pdf",
        string? fileName = "minna1.pdf") =>
        new(title, " みんなの日本語 ", null, DocCategory.Textbook, DocFileType.Pdf, key, fileName, 2048, [JlptLevel.N4, JlptLevel.N5, JlptLevel.N5],
            [JapaneseSkill.Grammar], [" giáo trình ", "Giáo trình", "", "N5"], Pages: 200, Publisher: "3A", Year: 2012);

    private static LibraryDocument New(DocAudience[]? audience = null, BranchId[]? branches = null) =>
        LibraryDocument.Create(Pdf(), audience ?? [DocAudience.Student, DocAudience.Teacher], branches ?? [], Staff, Now).Value;

    [Fact]
    public void TV02_tao_mac_dinh_watermark_chan_tai_va_chuan_hoa()
    {
        var d = New();
        (d.Watermark, d.AllowDownload, d.Status, d.IsStoredFile).ShouldBe((true, false, DocStatus.Published, true));
        (d.Subtitle, d.Levels.Count, d.Tags.Count).ShouldBe(("みんなの日本語", 2, 2));
        d.Tags.ShouldBe(["giáo trình", "N5"]);
    }

    [Fact]
    public void TV02_E1_E5_kiem_du_lieu()
    {
        LibraryDocument.Create(Pdf(" "), [DocAudience.Student], [], Staff, Now).Error.ShouldBe(LibraryDocumentErrors.TitleRequired);
        LibraryDocument.Create(Pdf(new string('x', 201)), [DocAudience.Student], [], Staff, Now).Error.ShouldBe(LibraryDocumentErrors.TitleTooLong);
        LibraryDocument.Create(Pdf(), [], [], Staff, Now).Error.ShouldBe(LibraryDocumentErrors.AudienceRequired);
        LibraryDocument.Create(Pdf(key: null), [DocAudience.Student], [], Staff, Now).Error.ShouldBe(LibraryDocumentErrors.FileRequired);
        LibraryDocument.Create(Pdf(fileName: "nghe.mp3"), [DocAudience.Student], [], Staff, Now).Error.ShouldBe(LibraryDocumentErrors.FileTypeMismatch);
        LibraryDocument.Create(Pdf() with { Year = 2028 }, [DocAudience.Student], [], Staff, Now).Error.ShouldBe(LibraryDocumentErrors.InvalidMeta);
        LibraryDocument.Create(Pdf() with { Pages = 0 }, [DocAudience.Student], [], Staff, Now).Error.ShouldBe(LibraryDocumentErrors.InvalidMeta);

        var link = Pdf() with { FileType = DocFileType.Link, FileKey = "ftp://x", FileName = null };
        LibraryDocument.Create(link, [DocAudience.Student], [], Staff, Now).Error.ShouldBe(LibraryDocumentErrors.InvalidLink);
        var ok = LibraryDocument.Create(link with { FileKey = " https://youtu.be/abc " }, [DocAudience.Student], [], Staff, Now).Value;
        (ok.IsStoredFile, ok.FileKey, ok.Size).ShouldBe((false, "https://youtu.be/abc", (long?)null));
    }

    [Fact]
    public void TV03_TV04_sua_an_va_bao_ve()
    {
        var d = New();
        d.Update(Pdf("Minna I (bản 2)", "library/documents/minna1-v2.pdf", "minna1-v2.PDF"), Now).IsSuccess.ShouldBeTrue();
        (d.Title, d.FileKey).ShouldBe(("Minna I (bản 2)", "library/documents/minna1-v2.pdf"));
        d.SetAccess([], []).Error.ShouldBe(LibraryDocumentErrors.AudienceRequired);
        d.SetProtection(false, true).ShouldBeTrue(); // vừa mở tải → ghi nhật ký
        d.SetProtection(false, true).ShouldBeFalse();
        d.Hide();
        d.Hide();
        d.Status.ShouldBe(DocStatus.Hidden);
    }

    [Fact]
    public void TV07_TV08_cong_chi_thay_dung_doi_tuong_chi_nhanh_dang_hien()
    {
        var all = New();
        all.IsVisibleOnPortal(DocAudience.Student, []).ShouldBeTrue(); // E1: không còn lớp vẫn thấy tài liệu chung
        all.IsVisibleOnPortal(DocAudience.Staff, [HaNoi]).ShouldBeFalse();

        var hn = New([DocAudience.Student], [HaNoi]);
        hn.IsVisibleOnPortal(DocAudience.Student, [HaNoi, Hcm]).ShouldBeTrue();
        hn.IsVisibleOnPortal(DocAudience.Student, [Hcm]).ShouldBeFalse();
        hn.IsVisibleOnPortal(DocAudience.Student, []).ShouldBeFalse();
        hn.IsForBranches([Hcm]).ShouldBeFalse();
        hn.Hide();
        hn.IsVisibleOnPortal(DocAudience.Student, [HaNoi]).ShouldBeFalse();
    }

    [Fact]
    public void TV05_luot_xem_gioi_han_phut_va_ket_thuc_1_lan()
    {
        var d = New();
        var v = DocumentView.Start(d.Id, ViewerType.Student, Guid.NewGuid(), " Trần An ", null, HaNoi, Now);
        v.BelongsTo(ViewerType.Guardian, v.ViewerId).ShouldBeFalse();
        v.End(Now.AddMinutes(12).AddSeconds(20)).ShouldBeTrue();
        (v.Minutes, v.ViewerName).ShouldBe((12.3m, "Trần An"));
        v.End(Now.AddHours(1)).ShouldBeFalse();
        v.Minutes.ShouldBe(12.3m);

        var forgot = DocumentView.Start(d.Id, ViewerType.Staff, Staff, "Lan", "Giáo vụ", null, Now);
        forgot.End(Now.AddDays(1));
        forgot.Minutes.ShouldBe(DocumentView.MaxMinutes);
        var clock = DocumentView.Start(d.Id, ViewerType.Staff, Staff, "Lan", "Giáo vụ", null, Now);
        clock.End(Now.AddMinutes(-5));
        clock.Minutes.ShouldBe(0m);
    }

    [Fact]
    public void TV06_thong_ke_14_ngay_top_va_phut_trung_binh()
    {
        var d = New();
        var an = Guid.NewGuid();
        var today = DateOnly.FromDateTime(Now.ToOffset(Vn).DateTime); // 24/11 giờ VN
        var views = new List<DocumentView>
        {
            Ended(DocumentView.Start(d.Id, ViewerType.Student, an, "An", null, HaNoi, Now), 10),
            Ended(DocumentView.Start(d.Id, ViewerType.Student, an, "An", null, HaNoi, Now.AddDays(-1)), 20),
            DocumentView.Start(d.Id, ViewerType.Student, an, "An", null, HaNoi, Now.AddDays(-20)), // ngoài 14 ngày, chưa kết thúc
            Ended(DocumentView.Start(d.Id, ViewerType.Staff, Staff, "Lan", "Giáo vụ", HaNoi, new DateTimeOffset(2026, 11, 23, 18, 0, 0, TimeSpan.Zero)), 6),
        };
        var s = LibraryStats.From(views, today, Vn);
        (s.Views, s.Readers, s.AvgMinutes, s.LastViewedAt).ShouldBe((4, 2, 12m, (DateTimeOffset?)Now));
        s.Daily.Count.ShouldBe(14);
        s.Daily[13].ShouldBe(2); // hôm nay: 1 lượt An + 1 lượt Lan lúc 01:00 giờ VN
        s.Daily[12].ShouldBe(1);
        s.Daily.Sum().ShouldBe(3);
        s.Top.ShouldBe([new TopReader("An", "Học viên", 3), new TopReader("Lan", "Giáo vụ", 1)]);

        d.ApplyStats(s);
        (d.ViewCount, d.ReaderCount, d.AvgMinutes).ShouldBe((4, 2, 12m));
        LibraryStats.From([], today, Vn).ShouldBe(LibraryStats.Empty);
    }

    [Fact]
    public void Watermark_ten_lien_he_thoi_diem()
    {
        Watermark.Text(" Trần An ", "an@mail.vn", new DateTime(2026, 11, 24, 10, 5, 0)).ShouldBe("Trần An · an@mail.vn · 24/11/2026 10:05");
        Watermark.Text("Lan", null, new DateTime(2026, 1, 2, 8, 0, 0)).ShouldBe("Lan · 02/01/2026 08:00");
    }

    private static DocumentView Ended(DocumentView v, int minutes)
    {
        v.End(v.StartedAt.AddMinutes(minutes));
        return v;
    }
}
