using JPCenter.Application.Common.Messaging;
using JPCenter.Modules.Academic.Application.Abstractions;
using JPCenter.Modules.Academic.Application.Exams.Common;
using JPCenter.Modules.Academic.Domain.Exams;
using JPCenter.SharedKernel.Domain;
using JPCenter.SharedKernel.Primitives;
using Microsoft.EntityFrameworkCore;

namespace JPCenter.Modules.Academic.Application.Exams.AutoSubmitExams;

/// <summary>ND-05.E4 — job mỗi phút: bài thi online quá giờ (+1 phút trễ mạng) mà HV chưa nộp → tự nộp các câu đã lưu, chấm, ghi kết quả</summary>
public sealed record AutoSubmitExamsCommand : ICommand<int>;

public sealed class AutoSubmitExamsHandler(IAcademicDbContext db, ExamGrader grader, IClock clock) : ICommandHandler<AutoSubmitExamsCommand, int>
{
    public async Task<Result<int>> Handle(AutoSubmitExamsCommand cmd, CancellationToken ct)
    {
        var now = clock.UtcNow;
        var cutoff = now - ExamAttempt.Grace;
        var expired = await db.ExamAttempts.Where(a => a.SubmittedAt == null && a.DeadlineAt < cutoff).Take(200).ToListAsync(ct);
        if (expired.Count == 0) return 0;
        var examIds = expired.Select(a => a.ClassExamId).Distinct().ToList();
        var exams = await db.ClassExams.Where(e => examIds.Contains(e.Id)).ToDictionaryAsync(e => e.Id, ct);
        var done = 0;
        foreach (var a in expired)
        {
            if ((await grader.SubmitAsync(a, exams[a.ClassExamId], now, ct)).IsSuccess) done++;
            await db.SaveChangesAsync(ct);
        }
        return done;
    }
}
