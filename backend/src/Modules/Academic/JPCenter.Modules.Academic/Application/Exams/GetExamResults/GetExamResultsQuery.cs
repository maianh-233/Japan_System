using JPCenter.Application.Common.Messaging;
using JPCenter.Application.Common.Security;
using JPCenter.Modules.Academic.Application.Abstractions;
using JPCenter.Modules.Academic.Application.Exams.Common;
using JPCenter.Modules.Students.Contracts;
using JPCenter.SharedKernel.Primitives;
using Microsoft.EntityFrameworkCore;

namespace JPCenter.Modules.Academic.Application.Exams.GetExamResults;

/// <summary>Trạng thái làm bài online của HV</summary>
public enum AttemptState
{
    /// <summary>Chưa vào thi (thi giấy luôn là trạng thái này)</summary>
    None,
    /// <summary>Đang làm</summary>
    InProgress,
    /// <summary>Đã nộp</summary>
    Submitted,
}

/// <summary>1 dòng trang Kết quả kỳ thi: HV + trạng thái làm bài + kết quả (null = chưa nhập)</summary>
public sealed record ExamResultRowDto(Guid StudentId, string Code, string FullName, AttemptState Attempt, ExamResultDto? Result);

public sealed record ExamResultsDto(ClassExamDto Exam, IReadOnlyList<ExamResultRowDto> Rows);

/// <summary>ND-06 — trang Kết quả kỳ thi (<c>ExamResultsPage</c>): danh sách lớp (đang học + bảo lưu) + HV đã có kết quả</summary>
public sealed record GetExamResultsQuery(Guid Id, bool AsTeacher) : IQuery<ExamResultsDto>;

public sealed class GetExamResultsHandler(IAcademicDbContext db, ExamFinder finder, ExamReader reader, IEnrollmentLookup enrollments)
    : IQueryHandler<GetExamResultsQuery, ExamResultsDto>
{
    public async Task<Result<ExamResultsDto>> Handle(GetExamResultsQuery q, CancellationToken ct)
    {
        var found = await finder.FindAsync(q.Id, q.AsTeacher, PermissionAction.View, ct);
        if (found.IsFailure) return found.Error;
        var (exam, cls) = found.Value;

        var roster = await enrollments.RosterAsync(cls.Id.Value, ct);
        var results = await db.ExamResults.AsNoTracking().Where(r => r.ClassId == exam.ClassId && r.ExamId == exam.ExamId)
            .ToDictionaryAsync(r => r.StudentId, ct);
        var attempts = await db.ExamAttempts.AsNoTracking().Where(a => a.ClassExamId == exam.Id)
            .Select(a => new { a.StudentId, a.SubmittedAt }).ToDictionaryAsync(a => a.StudentId, ct);
        var extra = results.Keys.Except(roster.Select(r => r.StudentId)).ToList();
        var names = extra.Count == 0 ? new Dictionary<Guid, string>() : await enrollments.NamesAsync(extra, ct);

        AttemptState StateOf(Guid s) => attempts.TryGetValue(s, out var a) ? a.SubmittedAt is null ? AttemptState.InProgress : AttemptState.Submitted
            : AttemptState.None;
        ExamResultDto? ResultOf(Guid s) => results.TryGetValue(s, out var r) ? ExamReader.ToDto(r) : null;

        var rows = roster.Select(r => new ExamResultRowDto(r.StudentId, r.Code, r.FullName, StateOf(r.StudentId), ResultOf(r.StudentId)))
            .Concat(extra.Select(s => new ExamResultRowDto(s, "", names.GetValueOrDefault(s, "—"), StateOf(s), ResultOf(s))))
            .ToList();
        return new ExamResultsDto(await reader.OneAsync(exam, cls, includeQuestions: false, ct), rows);
    }
}
