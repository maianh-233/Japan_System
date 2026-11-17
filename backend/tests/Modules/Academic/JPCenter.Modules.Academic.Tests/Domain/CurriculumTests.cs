using JPCenter.Modules.Academic.Domain.Curricula;
using JPCenter.SharedKernel.Primitives;

namespace JPCenter.Modules.Academic.Tests.Domain;

public sealed class CurriculumTests
{
    private static Result<Curriculum> Create(CurriculumTier tier = CurriculumTier.Standard, string? entry = null, string version = "v1.0",
        string code = "lt-n5") =>
        Curriculum.Create(code, "Lộ trình N5", JlptLevel.N5, tier, entry, version, "Minna I", null, ["JLPT", " jlpt ", ""], Guid.NewGuid());

    /// <summary>Lộ trình 3 bài, mỗi bài 1 chapter, mốc giữa kỳ sau bài 2</summary>
    private static (Curriculum C, Lesson[] L, ExamMilestone Mid) Sample()
    {
        var c = Create().Value;
        var lessons = new[] { "Bài 1", "Bài 2", "Bài 3" }.Select(t => c.AddLesson(t, false).Value).ToArray();
        foreach (var l in lessons) c.AddChapter(l.Id, "Từ vựng");
        var mid = c.AddExam(ExamKind.Midterm, "Giữa kỳ", lessons[1].Id, null).Value;
        return (c, lessons, mid);
    }

    [Fact]
    public void Tao_lo_trinh_chuan_hoa()
    {
        var c = Create().Value;
        c.Code.ShouldBe("LT-N5");
        c.Version.ShouldBe("v1.0");
        c.Tags.ShouldBe(["JLPT"]);
        c.Status.ShouldBe(CurriculumStatus.Draft);
    }

    [Fact]
    public void Quy_tac_tao()
    {
        Create(version: "1.0").Error.Code.ShouldBe("academic.curriculum.invalid_version");
        Create(code: "LT N5").Error.Code.ShouldBe("academic.curriculum.invalid_code");
        Create(CurriculumTier.Advanced).Error.Code.ShouldBe("academic.curriculum.entry_requirement_required");
        Create(CurriculumTier.Advanced, "Điểm test ≥ 80%").IsSuccess.ShouldBeTrue();
    }

    [Fact]
    public void Xoa_bai_chuyen_moc_thi_sang_bai_lien_truoc_va_danh_so_lai()
    {
        var (c, l, mid) = Sample();
        var removed = c.RemoveLesson(l[1].Id);
        removed.IsSuccess.ShouldBeTrue();
        mid.AfterLessonId.ShouldBe(l[0].Id);
        c.Lessons.OrderBy(x => x.Order).Select(x => x.Order).ShouldBe([1, 2]);
    }

    [Fact]
    public void Xoa_bai_dau_moc_thi_chuyen_sang_bai_sau_khong_con_bai_thi_xoa_moc()
    {
        var c = Create().Value;
        var only = c.AddLesson("Bài 1", false).Value;
        var second = c.AddLesson("Bài 2", false).Value;
        var exam = c.AddExam(ExamKind.Final, "Cuối kỳ", only.Id, null).Value;
        c.RemoveLesson(only.Id);
        exam.AfterLessonId.ShouldBe(second.Id);
        c.RemoveLesson(second.Id);
        c.Exams.ShouldBeEmpty();
    }

    [Fact]
    public void Doi_thu_tu_bai_phai_du_danh_sach()
    {
        var (c, l, _) = Sample();
        c.ReorderLessons([l[2].Id, l[0].Id]).Error.Code.ShouldBe("academic.curriculum.invalid_order");
        c.ReorderLessons([l[2].Id, l[0].Id, l[1].Id]).IsSuccess.ShouldBeTrue();
        l[2].Order.ShouldBe(1);
        l[1].Order.ShouldBe(3);
    }

    [Fact]
    public void Tai_lieu_file_va_link()
    {
        var (c, l, _) = Sample();
        var ch = l[0].Chapters[0];
        var file = c.AddFileMaterial(l[0].Id, ch.Id, null, "academic/materials/2026/11/abc.pptx", "Slide bài 1.pptx",
            "application/vnd.ms-powerpoint", 1200, false).Value;
        file.Name.ShouldBe("Slide bài 1");
        file.Type.ShouldBe(MaterialType.Ppt);
        file.IsFile.ShouldBeTrue();

        c.AddLinkMaterial(l[0].Id, ch.Id, "Video", MaterialType.Pdf, "https://youtu.be/x", true).Error.Code.ShouldBe("academic.material.link_type");
        c.AddLinkMaterial(l[0].Id, ch.Id, "Video", MaterialType.Video, "http://youtu.be/x", true).Error.Code.ShouldBe("academic.material.invalid_link");
        c.AddLinkMaterial(l[0].Id, ch.Id, "Drive", MaterialType.Drive, "https://dropbox.com/x", true).Error.Code.ShouldBe("academic.material.not_drive");
        var link = c.AddLinkMaterial(l[0].Id, ch.Id, "Flashcard", MaterialType.Drive, "https://drive.google.com/drive/folders/x", true).Value;
        link.IsFile.ShouldBeFalse();

        c.UpdateMaterial(file.Id, "Giáo án bài 1", false).IsSuccess.ShouldBeTrue();
        c.RemoveMaterial(file.Id).Value.FileKey.ShouldBe("academic/materials/2026/11/abc.pptx");
        c.FindMaterial(file.Id).ShouldBeNull();
    }

    [Fact]
    public void Phat_hanh_khoa_noi_dung()
    {
        Create().Value.Publish().Error.Code.ShouldBe("academic.curriculum.empty");
        var (c, l, _) = Sample();
        c.Publish().IsSuccess.ShouldBeTrue();
        c.AddLesson("Bài 4", false).Error.Code.ShouldBe("academic.curriculum.not_draft");
        c.RemoveLesson(l[0].Id).Error.Code.ShouldBe("academic.curriculum.not_draft");
        c.Publish().Error.Code.ShouldBe("academic.curriculum.not_draft");
    }

    [Fact]
    public void Ban_da_phat_hanh_chi_sua_mo_ta_va_the()
    {
        var (c, _, _) = Sample();
        c.Publish();
        c.UpdateInfo("LT-N5", "Lộ trình N5", JlptLevel.N5, CurriculumTier.Standard, null, "v1.0", "Minna I", "Mô tả mới", ["Mới"])
            .IsSuccess.ShouldBeTrue();
        c.Description.ShouldBe("Mô tả mới");
        c.UpdateInfo("LT-N5", "Tên khác", JlptLevel.N5, CurriculumTier.Standard, null, "v1.0", "Minna I", null, [])
            .Error.Code.ShouldBe("academic.curriculum.not_draft");
    }

    [Fact]
    public void Phien_ban_moi_sao_chep_noi_dung_dung_chung_file()
    {
        var (c, l, _) = Sample();
        c.AddFileMaterial(l[0].Id, l[0].Chapters[0].Id, "PDF", "k/1.pdf", "a.pdf", "application/pdf", 10, true);
        c.NewVersion(Guid.NewGuid()).Error.Code.ShouldBe("academic.curriculum.not_published");
        c.Publish();

        var v2 = c.NewVersion(Guid.NewGuid()).Value;
        v2.Version.ShouldBe("v1.1");
        v2.PreviousVersionId.ShouldBe(c.Id);
        v2.Status.ShouldBe(CurriculumStatus.Draft);
        v2.Lessons.Count.ShouldBe(3);
        v2.Lessons.ShouldAllBe(x => l.All(o => o.Id != x.Id));
        v2.Exams.Single().AfterLessonId.ShouldBe(v2.Lessons.Single(x => x.Title == "Bài 2").Id);
        v2.AllMaterials.Single().FileKey.ShouldBe("k/1.pdf");
    }

    [Fact]
    public void Nhan_ban_thanh_ban_nang_cao()
    {
        var (c, _, _) = Sample();
        var adv = c.CloneAs("LT-N5-ADV", "N5 nâng cao", JlptLevel.N5, CurriculumTier.Advanced, "Đầu vào ≥ 80%", "v1.0", null, null, [], Guid.NewGuid()).Value;
        adv.PreviousVersionId.ShouldBeNull();
        adv.Tier.ShouldBe(CurriculumTier.Advanced);
        adv.Lessons.Count.ShouldBe(3);
    }

    [Fact]
    public void Luu_tru_va_khoi_phuc()
    {
        var (c, _, _) = Sample();
        c.Archive().Error.Code.ShouldBe("academic.curriculum.not_published");
        c.Publish();
        c.Archive().IsSuccess.ShouldBeTrue();
        c.Restore().IsSuccess.ShouldBeTrue();
        c.Status.ShouldBe(CurriculumStatus.Published);
    }

    [Theory]
    [InlineData("v1.3", "v1.4")]
    [InlineData("v2.9", "v2.10")]
    [InlineData("1.0", "v1.1")]
    [InlineData("abc", "v1.0")]
    public void Phien_ban_ke_tiep(string current, string next) => Curriculum.NextVersion(current).ShouldBe(next);
}
