using JPCenter.Modules.Academic.Application.Abstractions;
using JPCenter.Modules.Academic.Application.Common;
using JPCenter.Modules.Academic.Application.Exams.Common;
using JPCenter.Modules.Academic.Domain.Classes;
using JPCenter.Modules.Academic.Domain.Curricula;
using JPCenter.Modules.Academic.Domain.Exams;
using JPCenter.Modules.Academic.Domain.Reviews;
using JPCenter.Modules.Students.Contracts;
using Microsoft.EntityFrameworkCore;

namespace JPCenter.Modules.Academic.Application.TeacherReviews.Common;

/// <summary>Đánh giá GV → DTO (tên GV, tên HV — ẩn khi <c>hideAnonymous</c>), đợt khảo sát đang mở của lớp</summary>
public sealed class TeacherReviewReader(IAcademicDbContext db, PeopleNames people, IEnrollmentLookup enrollments, ExamScope scope)
{
    public async Task<IReadOnlyList<TeacherReviewDto>> ToDtosAsync(IReadOnlyList<TeacherReview> list, bool hideAnonymous, CancellationToken ct)
    {
        if (list.Count == 0) return [];
        var classIds = list.Select(r => r.ClassId).Distinct().ToList();
        var codes = await db.Classes.AsNoTracking().IgnoreQueryFilters().Where(c => classIds.Contains(c.Id)).ToDictionaryAsync(c => c.Id, c => c.Code, ct);
        var teachers = await people.OfAsync(list.Select(r => (Guid?)r.TeacherId), ct);
        var visible = list.Where(r => !(hideAnonymous && r.Anonymous)).Select(r => r.StudentId).Distinct().ToList();
        var students = visible.Count == 0 ? new Dictionary<Guid, string>() : await enrollments.NamesAsync(visible, ct);
        return list.OrderByDescending(r => r.CreatedAt).Select(r =>
        {
            var hide = hideAnonymous && r.Anonymous;
            return new TeacherReviewDto(r.Id.Value, r.ClassId.Value, codes.GetValueOrDefault(r.ClassId, "—"), r.TeacherId,
                teachers.GetValueOrDefault(r.TeacherId, "—"), hide ? null : r.StudentId, hide ? null : students.GetValueOrDefault(r.StudentId),
                r.Period, r.Overall, new ReviewRatingsDto(r.Method, r.Clarity, r.Enthusiasm, r.Punctuality), r.Comment, r.Anonymous, r.CreatedAt);
        }).ToList();
    }

    /// <summary>ND-08 — đợt đang mở: giữa kỳ từ ngày thi giữa kỳ (kỳ thi đã phát hành của mốc giữa kỳ), cuối kỳ quanh ngày kết thúc lớp</summary>
    public async Task<ReviewPeriod?> OpenPeriodAsync(Class cls, DateOnly today, CancellationToken ct)
    {
        var cur = await scope.OfAsync(cls, ct);
        var mids = cur.Milestones.Where(m => m.Kind == ExamKind.Midterm).Select(m => m.Id).ToList();
        var midStart = mids.Count == 0 ? null : await db.ClassExams.AsNoTracking()
            .Where(e => e.ClassId == cls.Id && mids.Contains(e.ExamId) && e.Status == ClassExamStatus.Published)
            .OrderBy(e => e.StartsAt).Select(e => (DateTimeOffset?)e.StartsAt).FirstOrDefaultAsync(ct);
        return ReviewSurvey.OpenPeriod(today, midStart is { } m ? TimeText.DateOf(m) : null, cls.ActualEndDate ?? cls.PlannedEndDate);
    }
}
