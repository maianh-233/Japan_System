using JPCenter.Modules.Academic.Contracts;
using JPCenter.Modules.Academic.Contracts.Dtos;
using JPCenter.Modules.Academic.Domain.Classes;
using JPCenter.Modules.Academic.Domain.Exams;
using JPCenter.Modules.Academic.Infrastructure.Persistence;
using JPCenter.SharedKernel.Domain;
using Microsoft.EntityFrameworkCore;

namespace JPCenter.Modules.Academic.Infrastructure.Services;

/// <summary>
/// Hiện thực <see cref="ILearningStats"/> (HV-14, user story 12 quy tắc vàng 4) — giống <c>buildClassStats</c> ở frontend:
/// bài tới hạn = hạn nộp đã qua; nộp = có bài nộp; điểm TB = trung bình % điểm các bài đã có điểm; mỗi kết quả thi 1 % (vắng = null).
/// </summary>
internal sealed class LearningStats(AcademicDbContext db, IClock clock) : ILearningStats
{
    public async Task<IReadOnlyList<LearningStat>> ForClassesAsync(IReadOnlyCollection<Guid> classIds, IReadOnlyCollection<Guid>? studentIds,
        CancellationToken ct)
    {
        var ids = classIds.Distinct().Select(c => new ClassId(c)).ToList();
        if (ids.Count == 0) return [];
        var now = clock.UtcNow;

        var assignments = await db.Assignments.AsNoTracking().Where(a => ids.Contains(a.ClassId))
            .Select(a => new { a.Id, a.ClassId, a.DueDate, a.MaxScore }).ToListAsync(ct);
        var aIds = assignments.Select(a => a.Id).ToList();
        var subQuery = db.Submissions.AsNoTracking().Where(s => aIds.Contains(s.AssignmentId));
        if (studentIds is not null) subQuery = subQuery.Where(s => studentIds.Contains(s.StudentId));
        var submissions = await subQuery.Select(s => new { s.AssignmentId, s.StudentId, s.Score }).ToListAsync(ct);

        var resQuery = db.ExamResults.AsNoTracking().Where(r => ids.Contains(r.ClassId));
        if (studentIds is not null) resQuery = resQuery.Where(r => studentIds.Contains(r.StudentId));
        var results = await resQuery.ToListAsync(ct);
        // Thứ tự thi cũ → mới (giờ thi của lớp, không có thì lúc bắt đầu làm) — phần tử cuối = bài thi gần nhất (HV-15 "bài thi cuối")
        var examIds = results.Where(r => r.ClassExamId is not null).Select(r => r.ClassExamId!.Value).Distinct().ToList();
        var startsAt = await db.ClassExams.AsNoTracking().IgnoreQueryFilters().Where(e => examIds.Contains(e.Id))
            .ToDictionaryAsync(e => e.Id, e => e.StartsAt, ct);
        results = results.OrderBy(r => r.ClassExamId is { } ce && startsAt.TryGetValue(ce, out var at) ? at : r.StartedAt ?? DateTimeOffset.MinValue)
            .ThenBy(r => r.Id.Value).ToList();

        var byAssignment = assignments.ToDictionary(a => a.Id);
        var students = submissions.Select(s => (byAssignment[s.AssignmentId].ClassId, s.StudentId))
            .Concat(results.Select(r => (r.ClassId, r.StudentId)))
            .Concat(studentIds is null ? [] : ids.SelectMany(c => studentIds.Select(s => (c, s))))
            .Distinct().ToList();

        return students.Select(k =>
        {
            var (classId, studentId) = k;
            var due = assignments.Where(a => a.ClassId == classId && a.DueDate < now).Select(a => a.Id).ToHashSet();
            var mine = submissions.Where(s => s.StudentId == studentId && byAssignment[s.AssignmentId].ClassId == classId).ToList();
            var scored = mine.Where(s => s.Score is not null).Select(s => s.Score!.Value * 100 / byAssignment[s.AssignmentId].MaxScore).ToList();
            var exams = results.Where(r => r.ClassId == classId && r.StudentId == studentId)
                .Select(r => r.Status == ExamAttendance.Absent ? (int?)null : (int)Math.Round(r.Percent ?? 0)).ToList();
            return new LearningStat(classId.Value, studentId, due.Count, mine.Count(s => due.Contains(s.AssignmentId)),
                scored.Count == 0 ? null : (int)Math.Round(scored.Average()), exams);
        }).ToList();
    }
}
