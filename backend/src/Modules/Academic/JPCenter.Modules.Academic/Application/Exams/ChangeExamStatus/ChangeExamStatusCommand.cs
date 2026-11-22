using JPCenter.Application.Common.Messaging;
using JPCenter.Application.Common.Security;
using JPCenter.Modules.Academic.Application.Abstractions;
using JPCenter.Modules.Academic.Application.Common;
using JPCenter.Modules.Academic.Application.Exams.Common;
using JPCenter.Modules.Academic.Application.Learning.Common;
using JPCenter.Modules.Academic.Domain.Exams;
using JPCenter.SharedKernel.Domain;
using JPCenter.SharedKernel.Primitives;
using Microsoft.EntityFrameworkCore;

namespace JPCenter.Modules.Academic.Application.Exams.ChangeExamStatus;

/// <summary>Thao tác trạng thái kỳ thi</summary>
public enum ExamAction
{
    /// <summary>Phát hành: đề chốt; online → HV thấy ở Lịch thi (báo chuông HV)</summary>
    Publish,
    /// <summary>Hủy — đã có bài làm → <c>academic.exam.has_attempts</c> (ND-04.E5)</summary>
    Cancel,
    /// <summary>Công bố kết quả (ND-06) — HV / PH thấy điểm + nhận xét (báo chuông HV + PH). Thi online: sau giờ thi.</summary>
    PublishResults,
}

/// <summary>ND-04 / ND-06 — phát hành, hủy kỳ thi, công bố kết quả (giáo vụ <c>academic.classes.update</c>; công bố: cả GV của lớp)</summary>
public sealed record ChangeExamStatusCommand(Guid Id, ExamAction Action, bool AsTeacher = false) : ICommand<ClassExamDto>;

public sealed class ChangeExamStatusHandler(IAcademicDbContext db, ExamFinder finder, ExamReader reader, LearningAccess access,
    LearningNotices notices, IClock clock) : ICommandHandler<ChangeExamStatusCommand, ClassExamDto>
{
    public async Task<Result<ClassExamDto>> Handle(ChangeExamStatusCommand cmd, CancellationToken ct)
    {
        var found = await finder.FindAsync(cmd.Id, cmd.AsTeacher && cmd.Action == ExamAction.PublishResults, PermissionAction.Update, ct);
        if (found.IsFailure) return found.Error;
        var (exam, cls) = found.Value;
        var now = clock.UtcNow;

        switch (cmd.Action)
        {
            case ExamAction.Publish:
            {
                var r = exam.Publish(now);
                if (r.IsFailure) return r.Error;
                var students = await access.StudyingAsync(cls.Id, ct);
                var how = exam.Mode == ExamMode.Online ? "thi online trên Cổng" : "thi giấy tại lớp";
                await notices.SendAsync(new LearningReach(students, [], []), "Lịch thi mới",
                    $"{cls.Code}: {exam.Title} — {TimeText.Moment(exam.StartsAt)}, {exam.DurationMinutes} phút ({how})", "info", "academic.exam.published",
                    "/student/exams", "/parent/results", "/teacher/classes", ct);
                break;
            }
            case ExamAction.Cancel:
            {
                var r = exam.Cancel(await db.ExamAttempts.AnyAsync(a => a.ClassExamId == exam.Id, ct));
                if (r.IsFailure) return r.Error;
                break;
            }
            case ExamAction.PublishResults:
            {
                var first = !exam.ResultsPublished;
                var r = exam.PublishResults(now);
                if (r.IsFailure) return r.Error;
                if (first)
                {
                    var students = await db.ExamResults.AsNoTracking().Where(x => x.ClassId == exam.ClassId && x.ExamId == exam.ExamId)
                        .Select(x => x.StudentId).ToListAsync(ct);
                    await notices.ToLearnersAsync(students, "Đã có kết quả thi", $"{cls.Code}: {exam.Title}", "success",
                        "academic.exam.results_published", "/student/exams", "/parent/results", ct);
                }
                break;
            }
        }
        await db.SaveChangesAsync(ct);
        return await reader.OneAsync(exam, cls, includeQuestions: true, ct);
    }
}
