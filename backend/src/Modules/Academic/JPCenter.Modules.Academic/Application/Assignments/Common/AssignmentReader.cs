using JPCenter.Modules.Academic.Application.Abstractions;
using JPCenter.Modules.Academic.Application.Common;
using JPCenter.Modules.Academic.Application.Learning.Common;
using JPCenter.Modules.Academic.Domain.Assignments;
using JPCenter.Modules.Academic.Domain.Classes;
using Microsoft.EntityFrameworkCore;

namespace JPCenter.Modules.Academic.Application.Assignments.Common;

/// <summary>Đọc bài tập / bài nộp ra DTO kèm tên người giao / người chấm (1 lần cho cả danh sách)</summary>
public sealed class AssignmentReader(IAcademicDbContext db, PeopleNames people)
{
    public async Task<IReadOnlyList<AssignmentDto>> OfClassesAsync(IReadOnlyCollection<ClassId> classIds, CancellationToken ct)
    {
        var ids = classIds.Distinct().ToList();
        var list = await db.Assignments.AsNoTracking().Where(a => ids.Contains(a.ClassId)).OrderByDescending(a => a.AssignedAt).ToListAsync(ct);
        return await ToDtosAsync(list, ct);
    }

    public async Task<AssignmentDto> OneAsync(Assignment a, CancellationToken ct) => (await ToDtosAsync([a], ct))[0];

    public async Task<IReadOnlyList<AssignmentDto>> ToDtosAsync(IReadOnlyList<Assignment> list, CancellationToken ct)
    {
        var names = await people.OfAsync(list.Select(a => (Guid?)a.CreatedByTeacherId), ct);
        return list.Select(a => ToDto(a, names.GetValueOrDefault(a.CreatedByTeacherId, "—"))).ToList();
    }

    /// <summary>Bài nộp của các bài tập (<paramref name="studentId"/> = chỉ của 1 HV)</summary>
    public async Task<IReadOnlyList<SubmissionDto>> SubmissionsAsync(IReadOnlyCollection<Assignment> assignments, Guid? studentId,
        CancellationToken ct)
    {
        if (assignments.Count == 0) return [];
        var byId = assignments.ToDictionary(a => a.Id);
        var ids = byId.Keys.ToList();
        var q = db.Submissions.AsNoTracking().Where(s => ids.Contains(s.AssignmentId));
        if (studentId is { } sid) q = q.Where(s => s.StudentId == sid);
        var list = await q.OrderBy(s => s.SubmittedAt).ToListAsync(ct);
        return await SubmissionDtosAsync(list, byId, ct);
    }

    public async Task<SubmissionDto> OneAsync(Submission s, Assignment a, CancellationToken ct) =>
        (await SubmissionDtosAsync([s], new Dictionary<AssignmentId, Assignment> { [a.Id] = a }, ct))[0];

    private async Task<IReadOnlyList<SubmissionDto>> SubmissionDtosAsync(IReadOnlyList<Submission> list, IReadOnlyDictionary<AssignmentId, Assignment> byId,
        CancellationToken ct)
    {
        var names = await people.OfAsync(list.Select(s => s.GradedBy), ct);
        return list.Select(s => new SubmissionDto(s.Id.Value, s.AssignmentId.Value, s.StudentId, s.SubmittedAt, s.Files.Select(LearningFiles.ToDto).ToList(),
            s.QuizTotal is { } total ? new SubmissionQuizDto(s.QuizCorrect ?? 0, total, s.QuizAttempts, s.QuizDurationMinutes ?? 0) : null,
            s.Content, s.Score, s.Comment, s.GradedAt, s.GradedBy is { } by ? names.GetValueOrDefault(by, "—") : null,
            byId.TryGetValue(s.AssignmentId, out var a) && a.IsLate(s.SubmittedAt))).ToList();
    }

    public static AssignmentDto ToDto(Assignment a, string createdBy) => new(a.Id.Value, a.ClassId.Value, a.Type, a.Title, a.LessonId?.Value,
        a.Description, a.AssignedAt, a.DueDate, a.MaxScore,
        a.Quiz is { } q
            ? new QuizConfigDto(q.LessonIds.Select(l => l.Value).ToList(), q.Skills, q.Difficulty, q.QuestionCount, q.TimeLimitMinutes, q.Attempts, q.Shuffle)
            : null,
        a.Attachments.Select(LearningFiles.ToDto).ToList(), a.CreatedByTeacherId, createdBy);
}
