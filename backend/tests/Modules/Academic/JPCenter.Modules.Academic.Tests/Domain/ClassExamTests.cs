using JPCenter.Modules.Academic.Domain.Classes;
using JPCenter.Modules.Academic.Domain.Curricula;
using JPCenter.Modules.Academic.Domain.Exams;
using JPCenter.Modules.Academic.Domain.Questions;
using JPCenter.SharedKernel.Primitives;

namespace JPCenter.Modules.Academic.Tests.Domain;

/// <summary>ND-04 → ND-06 — kỳ thi theo mốc, bài làm online tự chấm, kết quả</summary>
public sealed class ClassExamTests
{
    private static readonly DateTimeOffset Start = new(2026, 12, 5, 1, 0, 0, TimeSpan.Zero);
    private static readonly ClassId Class = new(Guid.NewGuid());
    private static readonly ExamMilestoneId Milestone = new(Guid.NewGuid());
    private static readonly Guid Staff = Guid.NewGuid(), Student = Guid.NewGuid();

    private static readonly List<QuestionId> Vocab = Enumerable.Range(0, 4).Select(_ => QuestionId.New()).ToList();
    private static readonly List<QuestionId> Grammar = Enumerable.Range(0, 3).Select(_ => QuestionId.New()).ToList();

    private static List<ExamSection> Sections() =>
        [new(" Từ vựng ", JapaneseSkill.Vocab, 1, [.. Vocab]), new("Ngữ pháp", JapaneseSkill.Grammar, 2, [.. Grammar])];

    private static ClassExam Exam(ExamMode mode = ExamMode.Online, bool shuffle = true, bool publish = true)
    {
        var e = ClassExam.Create(Class, Milestone, "Giữa kỳ N5", mode, Start, 45, ClassExam.DefaultPassPercent, shuffle, Sections(), Staff).Value;
        if (publish) e.Publish(Start.AddDays(-1)).IsSuccess.ShouldBeTrue();
        return e;
    }

    [Fact]
    public void Tao_ky_thi_kiem_de_va_chot_de_khi_phat_hanh()
    {
        var e = Exam(publish: false);
        (e.MaxScore, e.QuestionCount, e.EndsAt, e.Sections[0].Name).ShouldBe((10m, 7, Start.AddMinutes(45), "Từ vựng"));

        ClassExam.Create(Class, Milestone, " ", ExamMode.Paper, Start, 45, 60, false, Sections(), Staff).Error.ShouldBe(ExamErrors.TitleRequired);
        ClassExam.Create(Class, Milestone, "x", ExamMode.Paper, Start, 5, 60, false, Sections(), Staff).Error.ShouldBe(ExamErrors.InvalidDuration);
        ClassExam.Create(Class, Milestone, "x", ExamMode.Paper, Start, 45, 0, false, Sections(), Staff).Error.ShouldBe(ExamErrors.InvalidPassPercent);
        ClassExam.Create(Class, Milestone, "x", ExamMode.Paper, Start, 45, 60, false, [], Staff).Error.ShouldBe(ExamErrors.Sections);
        ClassExam.Create(Class, Milestone, "x", ExamMode.Paper, Start, 45, 60, false, [new("A", null, 1, [Vocab[0]]), new("B", null, 1, [Vocab[0]])],
            Staff).Error.ShouldBe(ExamErrors.Sections);

        e.UpdateDraft("Giữa kỳ", ExamMode.Paper, Start, 60, 50, false, Sections()).IsSuccess.ShouldBeTrue();
        e.Publish(Start).IsSuccess.ShouldBeTrue();
        e.UpdateDraft("x", ExamMode.Paper, Start, 60, 50, false, Sections()).Error.ShouldBe(ExamErrors.Published);
        e.Reschedule(Start.AddDays(1), 90, hasAttempts: false).IsSuccess.ShouldBeTrue();
        e.Reschedule(Start, 90, hasAttempts: true).Error.ShouldBe(ExamErrors.HasAttempts);
        e.Cancel(hasAttempts: true).Error.ShouldBe(ExamErrors.HasAttempts);
        e.Cancel(hasAttempts: false).IsSuccess.ShouldBeTrue();
        e.Publish(Start).Error.ShouldBe(ExamErrors.Cancelled);
    }

    [Fact]
    public void Lam_bai_chi_trong_khung_gio_va_chi_thi_online()
    {
        var e = Exam();
        ExamAttempt.Start(e, Student, Start.AddMinutes(-1)).Error.ShouldBe(ExamErrors.NotOpen);
        ExamAttempt.Start(e, Student, e.EndsAt).Error.ShouldBe(ExamErrors.NotOpen);
        ExamAttempt.Start(Exam(ExamMode.Paper), Student, Start).Error.ShouldBe(ExamErrors.PaperExam);
        ExamAttempt.Start(Exam(publish: false), Student, Start).Error.ShouldBe(ExamErrors.NotOpen);

        var a = ExamAttempt.Start(e, Student, Start.AddMinutes(10)).Value;
        a.DeadlineAt.ShouldBe(e.EndsAt);
        a.Questions.Select(q => q.QuestionId).ShouldBe([.. Vocab, .. Grammar], ignoreOrder: true);
        a.Questions.Take(4).ShouldAllBe(q => q.Section == 0);
        a.Questions.ShouldAllBe(q => q.OptionOrder.OrderBy(x => x).SequenceEqual(new[] { 0, 1, 2, 3 }));
        // Cùng HV → cùng đề (mở lại tiếp tục được)
        ExamAttempt.Start(e, Student, Start.AddMinutes(11)).Value.Questions.Select(q => q.QuestionId).ShouldBe(a.Questions.Select(q => q.QuestionId));
    }

    [Fact]
    public void Tu_cham_tung_phan_theo_dap_an_goc_va_tu_nop_khi_het_gio()
    {
        var e = Exam(shuffle: true);
        var a = ExamAttempt.Start(e, Student, Start).Value;
        var key = a.Questions.ToDictionary(q => q.QuestionId, _ => 2);

        // Chọn đúng: vị trí hiển thị của đáp án gốc 2
        int PositionOf(QuestionId id, int original) => a.Questions.Single(q => q.QuestionId == id).OptionOrder.IndexOf(original);
        a.Answer(Vocab[0], PositionOf(Vocab[0], 2), Start).IsSuccess.ShouldBeTrue();
        a.Answer(Vocab[1], PositionOf(Vocab[1], 1), Start).IsSuccess.ShouldBeTrue();
        a.Answer(Grammar[0], PositionOf(Grammar[0], 2), Start).IsSuccess.ShouldBeTrue();
        a.Answer(Grammar[1], PositionOf(Grammar[1], 2), Start).IsSuccess.ShouldBeTrue();
        a.Answer(Grammar[1], null, Start).IsSuccess.ShouldBeTrue(); // bỏ chọn
        a.Answer(QuestionId.New(), 0, Start).Error.ShouldBe(ExamErrors.QuestionNotInExam);
        a.Answer(Vocab[2], 4, Start).Error.ShouldBe(ExamErrors.InvalidChoice);
        a.Answer(Vocab[2], 0, e.EndsAt.AddMinutes(2)).Error.ShouldBe(ExamErrors.NotOpen);

        var scores = a.Submit(e, key, e.EndsAt.AddMinutes(5)).Value;
        scores.Select(s => (s.Name, s.Score, s.Max)).ShouldBe([("Từ vựng", 1m, 4m), ("Ngữ pháp", 2m, 6m)]);
        (a.AutoSubmitted, a.SubmittedAt, a.DurationMinutes).ShouldBe((true, (DateTimeOffset?)e.EndsAt, 45));
        a.Submit(e, key, e.EndsAt).Error.ShouldBe(ExamErrors.AlreadySubmitted);
        a.Answer(Vocab[0], 0, Start).Error.ShouldBe(ExamErrors.AlreadySubmitted);
    }

    [Fact]
    public void Cong_bo_ket_qua_online_sau_gio_thi_va_sua_diem_vang_thi()
    {
        var e = Exam();
        e.PublishResults(Start.AddMinutes(10)).Error.ShouldBe(ExamErrors.NotFinished);
        e.PublishResults(e.EndsAt).IsSuccess.ShouldBeTrue();
        e.PublishResults(e.EndsAt.AddDays(1)).IsSuccess.ShouldBeTrue();
        e.ResultsPublishedAt.ShouldBe(e.EndsAt);
        Exam(ExamMode.Paper).PublishResults(Start).IsSuccess.ShouldBeTrue();
        Exam(publish: false).PublishResults(Start.AddDays(1)).Error.ShouldBe(ExamErrors.NotPublished);

        ExamResult.Record(Class, Milestone, e.Id, Student, [new("Nghe", 11, 10)], null, null, null).Error.ShouldBe(ExamErrors.InvalidScore);
        var r = ExamResult.Record(Class, Milestone, e.Id, Student, [new("Từ vựng", 3, 4), new("Ngữ pháp", 4, 6)], Start, 40, " Ổn ").Value;
        (r.TotalScore, r.MaxScore, r.Percent, r.Comment).ShouldBe((7m, 10m, (decimal?)70m, "Ổn"));
        r.MarkAbsent("Ốm");
        (r.Status, r.TotalScore, r.Percent, r.Comment).ShouldBe((ExamAttendance.Absent, 0m, (decimal?)null, "Ốm"));
        r.UpdateScores([new("Từ vựng", 4, 4)], null).IsSuccess.ShouldBeTrue();
        (r.Status, r.Percent).ShouldBe((ExamAttendance.Done, (decimal?)100m));
    }
}
