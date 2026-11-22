using JPCenter.Modules.Academic.Domain.Assignments;
using JPCenter.Modules.Academic.Domain.Classes;
using JPCenter.Modules.Academic.Domain.Curricula;
using JPCenter.SharedKernel.Primitives;

namespace JPCenter.Modules.Academic.Tests.Domain;

/// <summary>ND-01 → ND-03 — bài tập, bài nộp, chấm</summary>
public sealed class AssignmentTests
{
    private static readonly DateTimeOffset At = new(2026, 11, 20, 8, 0, 0, TimeSpan.Zero);
    private static readonly ClassId Class = new(Guid.NewGuid());
    private static readonly Guid Teacher = Guid.NewGuid(), Student = Guid.NewGuid();
    private static readonly LessonId Lesson = new(Guid.NewGuid());

    private static QuizConfig Quiz(int count = 10, int attempts = 2, int? limit = 15, List<LessonId>? lessons = null, List<JapaneseSkill>? skills = null) =>
        new(lessons ?? [Lesson, Lesson], skills ?? [JapaneseSkill.Vocab], QuizDifficulty.Mixed, count, limit, attempts, true);

    private static Result<Assignment> NewQuiz(QuizConfig? q = null, decimal max = 10) =>
        Assignment.Create(Class, AssignmentType.Quiz, " Từ vựng bài 5 ", Lesson, null, At, At.AddDays(3), max, q ?? Quiz(), [], Teacher);

    private static AttachedFile File(string name = "bai.jpg") => new(name, FileKind.Image, "academic/submissions/" + name, 1000);

    [Fact]
    public void Giao_bai_kiem_tra_han_thang_diem_va_tieu_chi_quiz()
    {
        var a = NewQuiz().Value;
        (a.Title, a.Quiz!.LessonIds.Count, a.Attachments.Count).ShouldBe(("Từ vựng bài 5", 1, 0));

        Assignment.Create(Class, AssignmentType.Upload, "x", null, null, At, At, 10, null, [], Teacher).Error.ShouldBe(AssignmentErrors.InvalidDue);
        Assignment.Create(Class, AssignmentType.Upload, " ", null, null, At, At.AddDays(1), 10, null, [], Teacher).Error
            .ShouldBe(AssignmentErrors.TitleRequired);
        NewQuiz(max: 0).Error.ShouldBe(AssignmentErrors.InvalidMaxScore);
        NewQuiz(max: 101).Error.ShouldBe(AssignmentErrors.InvalidMaxScore);
        NewQuiz(Quiz(count: 0)).Error.ShouldBe(AssignmentErrors.QuizConfig);
        NewQuiz(Quiz(count: 101)).Error.ShouldBe(AssignmentErrors.QuizConfig);
        NewQuiz(Quiz(attempts: 11)).Error.ShouldBe(AssignmentErrors.QuizConfig);
        NewQuiz(Quiz(limit: 0)).Error.ShouldBe(AssignmentErrors.QuizConfig);
        NewQuiz(Quiz(lessons: [])).Error.ShouldBe(AssignmentErrors.QuizConfig);
        NewQuiz(Quiz(skills: [])).Error.ShouldBe(AssignmentErrors.QuizConfig);

        // Bài upload bỏ tiêu chí quiz, giữ đề đính kèm
        var upload = Assignment.Create(Class, AssignmentType.Upload, "Viết", null, null, At, At.AddDays(1), 10, Quiz(), [File()], Teacher).Value;
        (upload.Quiz, upload.Attachments.Count).ShouldBe((null, 1));
    }

    [Fact]
    public void Co_bai_nop_thi_chi_sua_tieu_de_mo_ta_han()
    {
        var a = NewQuiz().Value;
        a.Edit("Mới", "Mô tả", At.AddDays(7)).IsSuccess.ShouldBeTrue();
        a.IsLate(At.AddDays(6)).ShouldBeFalse();
        a.IsLate(At.AddDays(8)).ShouldBeTrue();

        a.Reconfigure(AssignmentType.Quiz, Lesson, 10, Quiz(), [], hasSubmissions: true).IsSuccess.ShouldBeTrue();
        a.Reconfigure(AssignmentType.Quiz, Lesson, 10, Quiz(count: 20), [], hasSubmissions: true).Error.ShouldBe(AssignmentErrors.HasSubmissions);
        a.Reconfigure(AssignmentType.Upload, Lesson, 10, null, [], hasSubmissions: true).Error.ShouldBe(AssignmentErrors.HasSubmissions);
        a.Reconfigure(AssignmentType.Quiz, Lesson, 10, Quiz(count: 20), [], hasSubmissions: false).IsSuccess.ShouldBeTrue();
        a.Quiz!.QuestionCount.ShouldBe(20);
    }

    [Fact]
    public void Nop_file_nop_lai_truoc_khi_cham_va_cham_buoc_0_25()
    {
        var id = AssignmentId.New();
        Submission.SubmitFiles(id, Student, [], "  ", At).Error.ShouldBe(AssignmentErrors.SubmissionEmpty);
        Submission.SubmitFiles(id, Student, Enumerable.Range(0, 6).Select(i => File($"{i}.jpg")).ToList(), null, At).Error
            .ShouldBe(AssignmentErrors.TooManyFiles);

        var s = Submission.SubmitFiles(id, Student, [], " Bài làm ", At).Value;
        s.Content.ShouldBe("Bài làm");
        s.Resubmit([File()], null, At.AddHours(1)).IsSuccess.ShouldBeTrue();
        (s.Files.Count, s.Content, s.SubmittedAt).ShouldBe((1, (string?)null, At.AddHours(1)));

        s.Grade(10.1m, 10, null, Teacher, At).Error.ShouldBe(AssignmentErrors.InvalidScore);
        s.Grade(8.3m, 10, null, Teacher, At).Error.ShouldBe(AssignmentErrors.InvalidScore);
        s.Grade(8.25m, 10, " Tốt ", Teacher, At).IsSuccess.ShouldBeTrue();
        (s.Score, s.Comment, s.GradedBy).ShouldBe((8.25m, "Tốt", (Guid?)Teacher));
        s.Resubmit([File()], null, At).Error.ShouldBe(AssignmentErrors.AlreadyGraded);
    }

    [Fact]
    public void Quiz_giu_lan_cao_nhat_va_gioi_han_so_lan()
    {
        var s = Submission.SubmitQuiz(AssignmentId.New(), Student, 6, 10, 12, 10, maxAttempts: 2, At).Value;
        (s.Score, s.QuizAttempts, s.GradedBy).ShouldBe((6m, 1, (Guid?)null));
        s.RecordQuizAttempt(4, 10, 9, 10, 2, At.AddHours(1)).IsSuccess.ShouldBeTrue();
        (s.Score, s.QuizCorrect, s.QuizAttempts, s.SubmittedAt).ShouldBe((6m, (int?)6, 2, At));
        s.AttemptsLeft(2).ShouldBe(0);
        s.RecordQuizAttempt(10, 10, 5, 10, 2, At).Error.ShouldBe(AssignmentErrors.NoAttemptsLeft);

        // 7/9 × 10 = 7,77… → làm tròn xuống bước 0,25
        Submission.SubmitQuiz(AssignmentId.New(), Student, 7, 9, 5, 10, 1, At).Value.Score.ShouldBe(7.75m);

        // GV chấm đè điểm quiz → lần làm sau cao hơn không ghi đè điểm GV
        var graded = Submission.SubmitQuiz(AssignmentId.New(), Student, 5, 10, 5, 10, 3, At).Value;
        graded.Grade(9, 10, "Bù câu lỗi", Teacher, At).IsSuccess.ShouldBeTrue();
        graded.RecordQuizAttempt(8, 10, 5, 10, 3, At).IsSuccess.ShouldBeTrue();
        (graded.Score, graded.QuizCorrect).ShouldBe((9m, (int?)8));
    }
}
