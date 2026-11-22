using JPCenter.Application.Common.Messaging;
using JPCenter.Modules.Academic.Application.Abstractions;
using JPCenter.Modules.Academic.Application.Exams.Common;
using JPCenter.Modules.Academic.Application.Learning.Common;
using JPCenter.Modules.Academic.Domain.Exams;
using JPCenter.SharedKernel.Domain;
using JPCenter.SharedKernel.Primitives;
using Microsoft.EntityFrameworkCore;

namespace JPCenter.Modules.Academic.Application.Exams.StartExam;

/// <summary>
/// ND-05 — HV vào thi online: kỳ thi đang mở (E1), chưa nộp (E2). Lần đầu → tạo bài làm, đề riêng (đảo câu / đáp án theo HV);
/// mất mạng mở lại → tiếp tục bài đang làm, giờ vẫn chạy (E3). Quá giờ mà chưa nộp → hệ thống tự nộp các câu đã lưu (E4) rồi báo đã nộp.
/// </summary>
public sealed record StartExamCommand(Guid ExamId) : ICommand<AttemptPaperDto>;

public sealed class StartExamHandler(IAcademicDbContext db, LearningAccess access, MyAttempt mine, ExamGrader grader, IClock clock)
    : ICommandHandler<StartExamCommand, AttemptPaperDto>
{
    public async Task<Result<AttemptPaperDto>> Handle(StartExamCommand cmd, CancellationToken ct)
    {
        var learner = await access.LearnerAsync(null, ct);
        if (learner.IsFailure) return learner.Error;
        var exam = await db.ClassExams.FirstOrDefaultAsync(e => e.Id == new ClassExamId(cmd.ExamId), ct);
        if (exam is null || !learner.Value.Enrolled(exam.ClassId.Value) || exam.Status == ClassExamStatus.Draft) return ExamErrors.NotFound;
        if (!learner.Value.Studies(exam.ClassId.Value)) return LearningAccess.NotInClass;

        var me = learner.Value.StudentId;
        var now = clock.UtcNow;
        var attempt = await db.ExamAttempts.FirstOrDefaultAsync(a => a.ClassExamId == exam.Id && a.StudentId == me, ct);
        if (attempt is not null)
        {
            if (attempt.IsSubmitted) return ExamErrors.AlreadySubmitted;
            if (attempt.IsExpired(now))
            {
                var auto = await grader.SubmitAsync(attempt, exam, now, ct);
                if (auto.IsFailure) return auto.Error;
                await db.SaveChangesAsync(ct);
                return ExamErrors.AlreadySubmitted;
            }
            return await mine.PaperAsync(attempt, exam, ct);
        }

        var started = ExamAttempt.Start(exam, me, now);
        if (started.IsFailure) return started.Error;
        db.ExamAttempts.Add(started.Value);
        await db.SaveChangesAsync(ct);
        return await mine.PaperAsync(started.Value, exam, ct);
    }
}
