using JPCenter.Modules.Academic.Domain.Curricula;
using JPCenter.Modules.Academic.Domain.Questions;
using JPCenter.SharedKernel.Primitives;

namespace JPCenter.Modules.Academic.Tests.Domain;

public sealed class QuestionTests
{
    private static readonly Guid Author = Guid.NewGuid(), Reviewer = Guid.NewGuid();
    private static readonly DateTimeOffset At = new(2026, 11, 20, 8, 0, 0, TimeSpan.Zero);

    private static QuestionContent Content(JapaneseSkill skill = JapaneseSkill.Grammar, QuestionFormat format = QuestionFormat.Choice,
        string stem = "毎朝{まいあさ}コーヒー（　）飲{の}みます。", string? passage = null, IReadOnlyList<string>? options = null, int answer = 1) =>
        new(JlptLevel.N5, skill, format, QuestionDifficulty.Easy, stem, passage, null, options ?? ["が", "を", "に", "で"], answer, "を: tân ngữ",
            null, [], ["trợ từ", " ", "trợ từ"]);

    private static Question New(QuestionContent? c = null, bool submit = false) => Question.Create("N5-GR-1001", c ?? Content(), Author, submit).Value;

    private static Question Approved()
    {
        var q = New(submit: true);
        q.Approve(Reviewer, At).IsSuccess.ShouldBeTrue();
        return q;
    }

    [Fact]
    public void Tao_cau_nhap_chuan_hoa_noi_dung()
    {
        var q = New(Content(stem: "  毎朝コーヒー（　）飲みます。 ", options: [" が", "を ", "に", "で"]));
        q.Status.ShouldBe(QuestionStatus.Draft);
        q.Stem.ShouldBe("毎朝コーヒー（　）飲みます。");
        q.Options.ShouldBe(["が", "を", "に", "で"]);
        q.Tags.ShouldBe(["trợ từ"]);
        q.Picks.ShouldBe([0, 0, 0, 0]);
        q.CorrectRate.ShouldBeNull();
    }

    [Theory]
    [InlineData(3)]
    [InlineData(5)]
    public void NH_02_E2_can_dung_4_dap_an(int count) =>
        Question.Create("x", Content(options: Enumerable.Range(1, count).Select(i => $"đáp án {i}").ToList(), answer: 0), Author).Error
            .ShouldBe(QuestionErrors.Options);

    [Fact]
    public void NH_02_E2_dap_an_trung_nhau_bo_khoang_trang_va_hoa_thuong() =>
        Question.Create("x", Content(options: ["A b", "ab", "c", "d"]), Author).Error.ShouldBe(QuestionErrors.Options);

    [Fact]
    public void Dap_an_dung_ngoai_khoang_va_thieu_de_bai_va_cap_do()
    {
        Question.Create("x", Content(answer: 4), Author).Error.ShouldBe(QuestionErrors.AnswerInvalid);
        Question.Create("x", Content(stem: "  "), Author).Error.ShouldBe(QuestionErrors.StemRequired);
        Question.Create("x", Content() with { Level = JlptLevel.Beginner }, Author).Error.ShouldBe(QuestionErrors.InvalidLevel);
    }

    [Fact]
    public void NH_02_E3_E4_cau_sap_xep_can_sao_doc_hieu_can_doan_van()
    {
        Question.Create("x", Content(format: QuestionFormat.Star, stem: "わたしは＿＿＿＿＿＿です"), Author).Error.ShouldBe(QuestionErrors.StarMissing);
        Question.Create("x", Content(format: QuestionFormat.Star, stem: "わたしは＿＿★＿＿です"), Author).IsSuccess.ShouldBeTrue();
        Question.Create("x", Content(skill: JapaneseSkill.Reading), Author).Error.ShouldBe(QuestionErrors.PassageRequired);
    }

    [Fact]
    public void NH_02_E7_gan_bai_hoc_ma_khong_chon_lo_trinh() =>
        Question.Create("x", Content() with { LessonIds = [LessonId.New()] }, Author).Error.ShouldBe(QuestionErrors.LessonInvalid);

    [Fact]
    public void NH_02_E5_nghe_hieu_luu_nhap_duoc_nhung_gui_duyet_can_file_nghe()
    {
        var listening = Content(skill: JapaneseSkill.Listening) with { Transcript = "男：…" };
        Question.Create("x", listening, Author, submit: true).Error.ShouldBe(QuestionErrors.AudioRequired);
        var q = New(listening);
        q.Transcript.ShouldBe("男：…");
        q.Submit().Error.ShouldBe(QuestionErrors.AudioRequired);
        q.SetAudio("questions/a.mp3", "a.mp3", 75).IsSuccess.ShouldBeTrue();
        q.Submit().IsSuccess.ShouldBeTrue();
        q.Status.ShouldBe(QuestionStatus.Review);
    }

    [Fact]
    public void NH_04_file_nghe_chi_cho_nghe_hieu_va_tra_khoa_cu()
    {
        New().SetAudio("k", "a.mp3", 10).Error.ShouldBe(QuestionErrors.AudioNotListening);
        var q = New(Content(skill: JapaneseSkill.Listening));
        q.SetAudio("k1", "a.mp3", 0).Error.ShouldBe(QuestionErrors.AudioDuration);
        q.SetAudio("k1", "a.mp3", 10).Value.ShouldBeNull();
        q.SetAudio("k2", "b.mp3", 20).Value.ShouldBe("k1");
        (q.AudioKey, q.AudioName, q.AudioDurationSec).ShouldBe(("k2", "b.mp3", (int?)20));
    }

    [Fact]
    public void NH_05_nguoi_soan_khong_tu_duyet_tra_lai_can_ly_do()
    {
        var q = New(submit: true);
        q.Approve(Author, At).Error.ShouldBe(QuestionErrors.SelfReview);
        q.Return(Reviewer, At, " ").Error.ShouldBe(QuestionErrors.NoteRequired);
        q.Return(Reviewer, At, "Sai đáp án").IsSuccess.ShouldBeTrue();
        (q.Status, q.ReviewedBy, q.ReviewNote).ShouldBe((QuestionStatus.Draft, (Guid?)Reviewer, "Sai đáp án"));
        q.Approve(Reviewer, At).Error.ShouldBe(QuestionErrors.InvalidStatus);
        q.Submit().IsSuccess.ShouldBeTrue();
        q.Approve(Reviewer, At.AddHours(1)).IsSuccess.ShouldBeTrue();
        (q.Status, q.ReviewedAt).ShouldBe((QuestionStatus.Approved, (DateTimeOffset?)At.AddHours(1)));
    }

    [Fact]
    public void NH_03_sua_cau_da_duyet_ve_cho_duyet_doi_dap_an_dat_lai_thong_ke()
    {
        var q = Approved();
        q.RecordAnswer(1);
        q.RecordAnswer(2);
        q.RecordUse();

        q.Edit(Content() with { Explanation = "Giải thích mới" }, out _).IsSuccess.ShouldBeTrue();
        q.Status.ShouldBe(QuestionStatus.Review);
        q.Picks.ShouldBe([0, 1, 1, 0]); // đáp án không đổi → giữ thống kê

        q.Edit(Content(options: ["が", "を", "へ", "で"]), out _).IsSuccess.ShouldBeTrue();
        q.Picks.ShouldBe([0, 0, 0, 0]);
        q.UsedCount.ShouldBe(1);
    }

    [Fact]
    public void NH_03_doi_khoi_nghe_hieu_bo_file_nghe()
    {
        var q = New(Content(skill: JapaneseSkill.Listening));
        q.SetAudio("k1", "a.mp3", 10);
        q.Edit(Content(), out var removed).IsSuccess.ShouldBeTrue();
        removed.ShouldBe("k1");
        q.HasAudio.ShouldBeFalse();
    }

    [Fact]
    public void NH_06_ngung_dung_va_mo_lai()
    {
        var q = Approved();
        q.Retire(Reviewer, At, "").Error.ShouldBe(QuestionErrors.NoteRequired);
        q.Retire(Author, At, "Lộ đề").IsSuccess.ShouldBeTrue(); // ngừng dùng không cấm người soạn (có quyền duyệt)
        q.Status.ShouldBe(QuestionStatus.Retired);
        q.Edit(Content(), out _).Error.ShouldBe(QuestionErrors.Retired);
        q.Reopen().IsSuccess.ShouldBeTrue();
        q.Status.ShouldBe(QuestionStatus.Review);
        q.Reopen().Error.ShouldBe(QuestionErrors.InvalidStatus);
    }

    [Fact]
    public void NH_07_nhan_ban_ra_nhap_moi_dung_chung_file_nghe()
    {
        var q = New(Content(skill: JapaneseSkill.Listening));
        q.SetAudio("k1", "a.mp3", 10);
        q.Submit();
        q.Approve(Reviewer, At);
        q.RecordAnswer(1);
        var me = Guid.NewGuid();

        var copy = q.Duplicate("N5-LI-1002", me);
        (copy.Status, copy.AuthorId, copy.Code, copy.AudioKey).ShouldBe((QuestionStatus.Draft, me, "N5-LI-1002", "k1"));
        copy.Id.ShouldNotBe(q.Id);
        (copy.AttemptCount, copy.ReviewedBy).ShouldBe((0, (Guid?)null));
        copy.Options.ShouldBe(q.Options);
    }

    [Fact]
    public void NH_10_thong_ke_luot_lam()
    {
        var q = Approved();
        q.RecordAnswer(1);
        q.RecordAnswer(1);
        q.RecordAnswer(3);
        q.RecordAnswer(-1); // bỏ trống
        (q.AttemptCount, q.CorrectCount, q.CorrectRate).ShouldBe((4, 2, (int?)50));
        q.Picks.ShouldBe([0, 2, 0, 1]);
    }
}
