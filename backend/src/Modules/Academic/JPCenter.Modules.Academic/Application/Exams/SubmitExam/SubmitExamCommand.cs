using JPCenter.Application.Common.Messaging;
using JPCenter.Modules.Academic.Application.Abstractions;
using JPCenter.Modules.Academic.Application.Exams.Common;
using JPCenter.SharedKernel.Domain;
using JPCenter.SharedKernel.Primitives;

namespace JPCenter.Modules.Academic.Application.Exams.SubmitExam;

/// <summary>Đã nộp bài thi — KHÔNG có điểm (HV chỉ thấy điểm khi giáo vụ công bố kết quả — quy tắc vàng 2)</summary>
/// <param name="AutoSubmitted">Quá giờ, hệ thống tự nộp</param>
public sealed record ExamSubmittedDto(Guid AttemptId, DateTimeOffset SubmittedAt, bool AutoSubmitted, int Answered, int Total);

/// <summary>ND-05 — HV nộp bài thi online (1 lần — E2): chấm từng phần ở server, ghi kết quả</summary>
public sealed record SubmitExamCommand(Guid AttemptId) : ICommand<ExamSubmittedDto>;

public sealed class SubmitExamHandler(IAcademicDbContext db, MyAttempt mine, ExamGrader grader, IClock clock)
    : ICommandHandler<SubmitExamCommand, ExamSubmittedDto>
{
    public async Task<Result<ExamSubmittedDto>> Handle(SubmitExamCommand cmd, CancellationToken ct)
    {
        var found = await mine.FindAsync(cmd.AttemptId, ct);
        if (found.IsFailure) return found.Error;
        var (attempt, exam) = found.Value;
        var r = await grader.SubmitAsync(attempt, exam, clock.UtcNow, ct);
        if (r.IsFailure) return r.Error;
        await db.SaveChangesAsync(ct);
        return new ExamSubmittedDto(attempt.Id.Value, attempt.SubmittedAt!.Value, attempt.AutoSubmitted, attempt.Answers.Count, attempt.Questions.Count);
    }
}
