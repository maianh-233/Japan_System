using JPCenter.Application.Common.Messaging;
using JPCenter.Modules.Academic.Application.Abstractions;
using JPCenter.Modules.Academic.Application.Assignments.Common;
using JPCenter.Modules.Academic.Application.Learning.Common;
using JPCenter.Modules.Academic.Domain.Assignments;
using JPCenter.SharedKernel.Domain;
using JPCenter.SharedKernel.Primitives;
using Microsoft.EntityFrameworkCore;

namespace JPCenter.Modules.Academic.Application.Assignments.StartQuiz;

/// <summary>Đề quiz phát cho HV: vé nộp bài (đã ký) + câu hỏi KHÔNG đáp án</summary>
/// <param name="DeadlineAt">Hết giờ làm (null = không giới hạn) — nộp trễ quá 1 phút vẫn chấm phần đã trả lời, đánh dấu hết giờ (ND-02.E4)</param>
public sealed record QuizPaperDto(string Token, Guid AssignmentId, DateTimeOffset StartedAt, DateTimeOffset? DeadlineAt, int AttemptsLeft,
    IReadOnlyList<PaperQuestionDto> Questions);

/// <summary>
/// ND-02 — HV bấm làm quiz: còn lượt (E3), đang học lớp (E6) → server bốc đề từ câu đã duyệt (bài học → cùng trình độ → thấp hơn 1 bậc),
/// đảo đáp án khi bài bật đảo đề, trả câu hỏi không kèm đáp án (quy tắc vàng 2). Mỗi lần bấm là 1 đề mới; chỉ lần nộp mới tính lượt.
/// </summary>
public sealed record StartQuizCommand(Guid AssignmentId) : ICommand<QuizPaperDto>;

public sealed class StartQuizHandler(IAcademicDbContext db, LearningAccess access, QuizDrawer drawer, PaperBuilder paper, IQuizTokens tokens,
    IClock clock) : ICommandHandler<StartQuizCommand, QuizPaperDto>
{
    public static readonly Error NoQuestions = Error.Conflict("academic.assignment.no_questions",
        "Ngân hàng câu hỏi chưa có câu đã duyệt phù hợp — báo giáo viên");

    public async Task<Result<QuizPaperDto>> Handle(StartQuizCommand cmd, CancellationToken ct)
    {
        var learner = await access.LearnerAsync(null, ct);
        if (learner.IsFailure) return learner.Error;
        var a = await db.Assignments.AsNoTracking().FirstOrDefaultAsync(x => x.Id == new AssignmentId(cmd.AssignmentId), ct);
        if (a is null || !learner.Value.Enrolled(a.ClassId.Value)) return AssignmentErrors.NotFound;
        if (a.Quiz is not { } quiz) return AssignmentErrors.WrongType;
        if (!learner.Value.Studies(a.ClassId.Value)) return LearningAccess.NotInClass;

        var me = learner.Value.StudentId;
        var done = await db.Submissions.AsNoTracking().Where(s => s.AssignmentId == a.Id && s.StudentId == me).Select(s => s.QuizAttempts)
            .FirstOrDefaultAsync(ct);
        if (done >= quiz.Attempts) return AssignmentErrors.NoAttemptsLeft;

        var cls = await db.Classes.AsNoTracking().IgnoreQueryFilters().FirstAsync(c => c.Id == a.ClassId, ct);
        var (level, _) = await drawer.CurriculumOfAsync(cls, ct);
        var drawn = await drawer.DrawAsync(level, quiz, ct);
        if (drawn.Count == 0) return NoQuestions;

        var rng = quiz.Shuffle ? new Random() : null;
        var slots = drawn.Select(q => new PaperSlot(q.Id, 0, PaperBuilder.Order(q.Options.Count, rng))).ToList();
        var now = clock.UtcNow;
        var token = tokens.Issue(new QuizTicket(a.Id.Value, me, done, now,
            slots.Select(s => new QuizTicketQuestion(s.QuestionId, s.OptionOrder)).ToList()));
        DateTimeOffset? deadline = quiz.TimeLimitMinutes is { } m ? now.AddMinutes(m) : null;
        return new QuizPaperDto(token, a.Id.Value, now, deadline, quiz.Attempts - done, await paper.PaperAsync(slots, ct));
    }
}
