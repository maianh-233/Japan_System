using FluentValidation;
using JPCenter.Application.Common.Messaging;
using JPCenter.Modules.Academic.Application.Abstractions;
using JPCenter.Modules.Academic.Application.Exams.Common;
using JPCenter.Modules.Academic.Domain.Questions;
using JPCenter.SharedKernel.Domain;
using JPCenter.SharedKernel.Primitives;

namespace JPCenter.Modules.Academic.Application.Exams.SaveExamAnswers;

/// <param name="Choice">Vị trí đáp án HIỂN THỊ (0-based); null = bỏ chọn</param>
public sealed record ExamAnswerInput(Guid QuestionId, int? Choice);

/// <summary>ND-05 — tự lưu câu đã chọn (gửi 1 hoặc nhiều câu 1 lần). Đã nộp / quá giờ (+1 phút trễ mạng) → chặn.</summary>
public sealed record SaveExamAnswersCommand(Guid AttemptId, IReadOnlyList<ExamAnswerInput> Answers) : ICommand;

public sealed class SaveExamAnswersValidator : AbstractValidator<SaveExamAnswersCommand>
{
    public SaveExamAnswersValidator() => RuleFor(x => x.Answers).NotNull().Must(a => a is null || a.Count <= 500);
}

public sealed class SaveExamAnswersHandler(IAcademicDbContext db, MyAttempt mine, IClock clock) : ICommandHandler<SaveExamAnswersCommand>
{
    public async Task<Result> Handle(SaveExamAnswersCommand cmd, CancellationToken ct)
    {
        var found = await mine.FindAsync(cmd.AttemptId, ct);
        if (found.IsFailure) return found.Error;
        var (attempt, _) = found.Value;
        var now = clock.UtcNow;
        foreach (var a in cmd.Answers)
            if (attempt.Answer(new QuestionId(a.QuestionId), a.Choice, now) is { IsFailure: true } r) return r;
        await db.SaveChangesAsync(ct);
        return Result.Success();
    }
}
