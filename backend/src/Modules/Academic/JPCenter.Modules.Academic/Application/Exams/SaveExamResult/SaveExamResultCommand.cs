using FluentValidation;
using JPCenter.Application.Common.Auditing;
using JPCenter.Application.Common.Messaging;
using JPCenter.Application.Common.Security;
using JPCenter.Modules.Academic.Application.Abstractions;
using JPCenter.Modules.Academic.Application.Exams.Common;
using JPCenter.Modules.Academic.Domain.Exams;
using JPCenter.Modules.Students.Contracts;
using JPCenter.SharedKernel.Primitives;
using Microsoft.EntityFrameworkCore;

namespace JPCenter.Modules.Academic.Application.Exams.SaveExamResult;

/// <summary>
/// ND-06 — GV của lớp / giáo vụ nhập hoặc sửa kết quả 1 HV: điểm từng phần theo thứ tự phần của đề (0 → tối đa phần — E1) hoặc vắng thi (E3) + nhận xét.
/// Thi online: sửa điểm tự chấm (bù câu lỗi). Kỳ thi phải đã phát hành. Sửa sau khi công bố → ghi nhật ký trước → sau (E2).
/// </summary>
public sealed record SaveExamResultCommand(Guid ExamId, Guid StudentId, bool AsTeacher, bool Absent, IReadOnlyList<decimal>? Scores, string? Comment)
    : ICommand<ExamResultDto>;

public sealed class SaveExamResultValidator : AbstractValidator<SaveExamResultCommand>
{
    public SaveExamResultValidator() => RuleFor(x => x.Comment).MaximumLength(2000);
}

public sealed class SaveExamResultHandler(IAcademicDbContext db, ExamFinder finder, IEnrollmentLookup enrollments, IAuditTrail audit)
    : ICommandHandler<SaveExamResultCommand, ExamResultDto>
{
    public static readonly Error NotInClass = Error.Validation("academic.exam.student_not_in_class", "Học viên không học lớp này");

    public async Task<Result<ExamResultDto>> Handle(SaveExamResultCommand cmd, CancellationToken ct)
    {
        var found = await finder.FindAsync(cmd.ExamId, cmd.AsTeacher, PermissionAction.Update, ct);
        if (found.IsFailure) return found.Error;
        var (exam, cls) = found.Value;
        if (exam.Status == ClassExamStatus.Cancelled) return ExamErrors.Cancelled;
        if (exam.Status != ClassExamStatus.Published) return ExamErrors.NotPublished;

        var result = await db.ExamResults.FirstOrDefaultAsync(r => r.ClassId == exam.ClassId && r.ExamId == exam.ExamId && r.StudentId == cmd.StudentId, ct);
        if (result is null && !(await enrollments.RosterAsync(cls.Id.Value, ct)).Any(r => r.StudentId == cmd.StudentId)) return NotInClass;
        var before = result is null ? "—" : Describe(result);

        List<ExamSectionScore> sections = [];
        if (!cmd.Absent)
        {
            var scores = cmd.Scores ?? [];
            if (scores.Count != exam.Sections.Count) return ExamErrors.InvalidScore;
            sections = exam.Sections.Select((s, i) => new ExamSectionScore(s.Name, scores[i], s.Max)).ToList();
        }

        if (result is null)
        {
            if (cmd.Absent)
            {
                result = ExamResult.Absent(exam.ClassId, exam.ExamId, exam.Id, cmd.StudentId);
                result.UpdateComment(cmd.Comment);
            }
            else
            {
                var created = ExamResult.Record(exam.ClassId, exam.ExamId, exam.Id, cmd.StudentId, sections, null, null, cmd.Comment);
                if (created.IsFailure) return created.Error;
                result = created.Value;
            }
            db.ExamResults.Add(result);
        }
        else if (cmd.Absent) result.MarkAbsent(cmd.Comment);
        else if (result.UpdateScores(sections, cmd.Comment) is { IsFailure: true } r) return r.Error;

        await db.SaveChangesAsync(ct);
        if (exam.ResultsPublished)
            audit.Record(AuditCategory.Data, AuditSeverity.Warning, "Sửa điểm thi sau công bố", $"{cls.Code} · {exam.Title}",
                $"HV {cmd.StudentId}: {before} → {Describe(result)}");
        return ExamReader.ToDto(result);
    }

    private static string Describe(ExamResult r) =>
        r.Status == ExamAttendance.Absent ? "vắng thi" : $"{r.TotalScore:0.##}/{r.MaxScore:0.##} ({string.Join(", ", r.Sections.Select(s => $"{s.Name} {s.Score:0.##}"))})";
}
