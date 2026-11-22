using FluentValidation;
using JPCenter.Application.Common.Messaging;
using JPCenter.Modules.Academic.Application.Abstractions;
using JPCenter.Modules.Academic.Application.Assignments.Common;
using JPCenter.Modules.Academic.Application.Learning.Common;
using JPCenter.Modules.Academic.Domain.Assignments;
using JPCenter.Modules.Academic.Domain.Exams;
using JPCenter.SharedKernel.Domain;
using JPCenter.SharedKernel.Primitives;
using Microsoft.EntityFrameworkCore;

namespace JPCenter.Modules.Academic.Application.Assignments.SubmitQuiz;

/// <summary>1 câu trả lời: vị trí đáp án HIỂN THỊ (0-based), null = bỏ trống</summary>
public sealed record QuizAnswerInput(Guid QuestionId, int? Choice);

/// <summary>Kết quả 1 lần làm quiz + đáp án / giải thích (quiz bài tập xem đáp án ngay sau khi nộp — quy tắc vàng 2)</summary>
/// <param name="Submission">Bài nộp sau khi ghi (điểm = lần cao nhất)</param>
public sealed record QuizResultDto(int Correct, int Total, decimal Score, decimal MaxScore, bool TimedOut, SubmissionDto Submission,
    IReadOnlyList<ReviewQuestionDto> Review);

/// <summary>
/// ND-02 — HV nộp quiz bằng vé đã phát: server chấm theo đáp án trong ngân hàng, lưu lần điểm cao nhất (hết lượt → E3), quá giới hạn phút
/// (+1 phút trễ mạng) → chấm phần đã trả lời, đánh dấu hết giờ (E4). 1 vé chỉ nộp 1 lần. Ghi thống kê lượt làm của câu hỏi (NH-10).
/// </summary>
public sealed record SubmitQuizCommand(Guid AssignmentId, string Token, IReadOnlyList<QuizAnswerInput>? Answers) : ICommand<QuizResultDto>;

public sealed class SubmitQuizValidator : AbstractValidator<SubmitQuizCommand>
{
    public SubmitQuizValidator()
    {
        RuleFor(x => x.Token).NotEmpty();
        RuleFor(x => x.Answers).Must(a => a is null || a.Count <= Assignment.MaxQuestions);
    }
}

public sealed class SubmitQuizHandler(IAcademicDbContext db, LearningAccess access, PaperBuilder paper, IQuizTokens tokens, AssignmentReader reader,
    IClock clock) : ICommandHandler<SubmitQuizCommand, QuizResultDto>
{
    public static readonly Error InvalidTicket = Error.Validation("academic.quiz.invalid_ticket", "Đề làm bài không hợp lệ hoặc đã quá hạn — bấm làm lại");
    public static readonly Error TicketUsed = Error.Conflict("academic.quiz.ticket_used", "Lượt làm này đã nộp — bấm làm bài để làm lượt mới");

    public async Task<Result<QuizResultDto>> Handle(SubmitQuizCommand cmd, CancellationToken ct)
    {
        var learner = await access.LearnerAsync(null, ct);
        if (learner.IsFailure) return learner.Error;
        var me = learner.Value.StudentId;
        var ticket = tokens.Read(cmd.Token);
        if (ticket is null || ticket.AssignmentId != cmd.AssignmentId || ticket.StudentId != me) return InvalidTicket;

        var a = await db.Assignments.AsNoTracking().FirstOrDefaultAsync(x => x.Id == new AssignmentId(cmd.AssignmentId), ct);
        if (a is null || !learner.Value.Enrolled(a.ClassId.Value)) return AssignmentErrors.NotFound;
        if (a.Quiz is not { } quiz) return AssignmentErrors.WrongType;
        if (!learner.Value.Studies(a.ClassId.Value)) return LearningAccess.NotInClass;

        var existing = await db.Submissions.FirstOrDefaultAsync(s => s.AssignmentId == a.Id && s.StudentId == me, ct);
        if ((existing?.QuizAttempts ?? 0) != ticket.AttemptNo) return TicketUsed;
        if (ticket.AttemptNo >= quiz.Attempts) return AssignmentErrors.NoAttemptsLeft;

        // Vị trí hiển thị → chỉ số đáp án gốc
        var slots = ticket.Questions.Select(q => new PaperSlot(q.QuestionId, 0, q.OptionOrder)).ToList();
        var bySlot = slots.ToDictionary(s => s.QuestionId);
        var chosen = new Dictionary<Guid, int>();
        foreach (var ans in cmd.Answers ?? [])
        {
            if (ans.Choice is not { } pos || !bySlot.TryGetValue(ans.QuestionId, out var slot)) continue;
            if (pos < 0 || pos >= slot.OptionOrder.Count) return ExamErrors.InvalidChoice;
            chosen[ans.QuestionId] = slot.OptionOrder[pos];
        }

        var now = clock.UtcNow;
        var timedOut = quiz.TimeLimitMinutes is { } limit && now > ticket.StartedAt.AddMinutes(limit) + ExamAttempt.Grace;
        var minutes = (int)Math.Ceiling((now - ticket.StartedAt).TotalMinutes);
        if (quiz.TimeLimitMinutes is { } cap) minutes = Math.Min(minutes, cap);

        var review = await paper.GradeAsync(slots, chosen, recordStats: true, ct);
        var correct = review.Count(r => r.Correct);
        var total = slots.Count;

        Submission s;
        if (existing is null)
        {
            var created = Submission.SubmitQuiz(a.Id, me, correct, total, minutes, a.MaxScore, quiz.Attempts, now);
            if (created.IsFailure) return created.Error;
            s = created.Value;
            db.Submissions.Add(s);
        }
        else
        {
            var r = existing.RecordQuizAttempt(correct, total, minutes, a.MaxScore, quiz.Attempts, now);
            if (r.IsFailure) return r.Error;
            s = existing;
        }
        await db.SaveChangesAsync(ct);

        var thisScore = total == 0 ? 0 : decimal.Floor(a.MaxScore * correct / total / Submission.ScoreStep) * Submission.ScoreStep;
        return new QuizResultDto(correct, total, thisScore, a.MaxScore, timedOut, await reader.OneAsync(s, a, ct), review);
    }
}
