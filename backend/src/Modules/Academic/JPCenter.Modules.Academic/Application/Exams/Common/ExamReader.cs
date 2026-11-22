using JPCenter.Modules.Academic.Application.Abstractions;
using JPCenter.Modules.Academic.Domain.Classes;
using JPCenter.Modules.Academic.Domain.Curricula;
using JPCenter.Modules.Academic.Domain.Exams;
using Microsoft.EntityFrameworkCore;

namespace JPCenter.Modules.Academic.Application.Exams.Common;

/// <summary>Đọc kỳ thi / kết quả ra DTO (kèm loại mốc, số bài làm, số kết quả)</summary>
public sealed class ExamReader(IAcademicDbContext db, ExamScope scope)
{
    public async Task<ClassExamBoardDto> BoardAsync(Class cls, CancellationToken ct)
    {
        var cur = await scope.OfAsync(cls, ct);
        var exams = await db.ClassExams.AsNoTracking().Where(e => e.ClassId == cls.Id).OrderBy(e => e.StartsAt).ToListAsync(ct);
        var dtos = await ToDtosAsync(exams, cur, includeQuestions: true, ct);
        var results = await db.ExamResults.AsNoTracking().Where(r => r.ClassId == cls.Id).ToListAsync(ct);
        var milestones = cur.Milestones.Select(m => new ExamMilestoneDto(m.Id.Value, m.Kind, m.Title, m.AfterLessonId.Value,
            cur.Scopes[m.Id].Select(l => l.Value).ToList(),
            exams.FirstOrDefault(e => e.ExamId == m.Id && e.Status != ClassExamStatus.Cancelled)?.Id.Value)).ToList();
        return new ClassExamBoardDto(milestones, dtos, results.Select(ToDto).ToList());
    }

    public async Task<ClassExamDto> OneAsync(ClassExam exam, Class cls, bool includeQuestions, CancellationToken ct) =>
        (await ToDtosAsync([exam], await scope.OfAsync(cls, ct), includeQuestions, ct))[0];

    public async Task<IReadOnlyList<ClassExamDto>> ToDtosAsync(IReadOnlyList<ClassExam> exams, ClassCurriculum? cur, bool includeQuestions,
        CancellationToken ct)
    {
        if (exams.Count == 0) return [];
        var ids = exams.Select(e => e.Id).ToList();
        var attempts = await db.ExamAttempts.AsNoTracking().Where(a => ids.Contains(a.ClassExamId)).GroupBy(a => a.ClassExamId)
            .Select(g => new { g.Key, N = g.Count() }).ToDictionaryAsync(x => x.Key, x => x.N, ct);
        var nullable = ids.Select(i => (ClassExamId?)i).ToList();
        var results = await db.ExamResults.AsNoTracking().Where(r => nullable.Contains(r.ClassExamId)).GroupBy(r => r.ClassExamId)
            .Select(g => new { g.Key, N = g.Count() }).ToDictionaryAsync(x => x.Key!.Value, x => x.N, ct);
        return exams.Select(e => ToDto(e, cur?.Find(e.ExamId)?.Kind, attempts.GetValueOrDefault(e.Id), results.GetValueOrDefault(e.Id), includeQuestions))
            .ToList();
    }

    public static ClassExamDto ToDto(ClassExam e, ExamKind? kind, int attempts, int results, bool includeQuestions) => new(e.Id.Value, e.ClassId.Value,
        e.ExamId.Value, kind, e.Title, e.Mode, e.StartsAt, e.EndsAt, e.DurationMinutes, e.PassPercent, e.Shuffle,
        e.Sections.Select(s => new ExamSectionDto(s.Name, s.Skill, s.Points, s.QuestionIds.Count, s.Max,
            includeQuestions ? s.QuestionIds.Select(q => q.Value).ToList() : [])).ToList(),
        e.MaxScore, e.QuestionCount, e.Status, e.PublishedAt, e.ResultsPublishedAt, attempts, results);

    public static ExamResultDto ToDto(ExamResult r) => new(r.Id.Value, r.ClassId.Value, r.ExamId.Value, r.ClassExamId?.Value, r.StudentId, r.Status,
        r.Sections, r.TotalScore, r.MaxScore, r.Percent, r.StartedAt, r.DurationMinutes, r.Comment);
}
