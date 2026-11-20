using FluentValidation;
using JPCenter.Application.Common.Messaging;
using JPCenter.Modules.Academic.Application.Abstractions;
using JPCenter.Modules.Academic.Application.Evaluations.Common;
using JPCenter.Modules.Academic.Application.Portals.Common;
using JPCenter.Modules.Academic.Domain.Classes;
using JPCenter.Modules.Academic.Domain.ClassSessions;
using JPCenter.Modules.Academic.Domain.Reviews;
using JPCenter.Modules.Students.Contracts;
using JPCenter.SharedKernel.Domain;
using JPCenter.SharedKernel.Primitives;
using Microsoft.EntityFrameworkCore;

namespace JPCenter.Modules.Academic.Application.Evaluations.SaveEvaluation;

/// <summary>
/// DT-19 — GV của lớp ghi / sửa nhận xét cuối khóa 1 HV (UQ lớp × HV): 5 kỹ năng + thái độ 1–5 sao (E1), điểm mạnh / yếu, đề xuất*, ghi chú cho Sale.
/// Sửa được tới khi lớp kết thúc + 14 ngày.
/// </summary>
public sealed record SaveEvaluationCommand(Guid ClassId, Guid StudentId, EvalSkillsDto Skills, int Attitude, string? Strengths, string? Weaknesses,
    EvalRecommendation Recommendation, string? Advice) : ICommand<EvaluationDto>;

public sealed class SaveEvaluationValidator : AbstractValidator<SaveEvaluationCommand>
{
    public SaveEvaluationValidator()
    {
        RuleFor(x => x.Skills).NotNull();
        RuleFor(x => x.Recommendation).IsInEnum().WithMessage("Chọn đề xuất bước học tiếp");
        RuleFor(x => x.Strengths).MaximumLength(2000);
        RuleFor(x => x.Weaknesses).MaximumLength(2000);
        RuleFor(x => x.Advice).MaximumLength(2000);
    }
}

public sealed class SaveEvaluationHandler(IAcademicDbContext db, PortalOwner owner, IEnrollmentLookup enrollments, EvaluationReader reader, IClock clock)
    : ICommandHandler<SaveEvaluationCommand, EvaluationDto>
{
    public static readonly Error NotInClass = Error.Validation("academic.evaluation.not_in_class", "Học viên không học lớp này");

    public async Task<Result<EvaluationDto>> Handle(SaveEvaluationCommand cmd, CancellationToken ct)
    {
        var cls = await db.Classes.AsNoTracking().FirstOrDefaultAsync(c => c.Id == new ClassId(cmd.ClassId), ct);
        if (cls is null) return ClassErrors.NotFound;
        if (owner.TeacherId is not { } me || !cls.TeacherIds.Contains(me)) return SessionErrors.NotAssigned;
        if (!(await enrollments.OfStudentAsync(cmd.StudentId, ct)).Any(e => e.ClassId == cls.Id.Value)) return NotInClass;

        var s = cmd.Skills;
        var scores = new EvaluationScores(s.Vocab, s.Grammar, s.Reading, s.Listening, s.Speaking, cmd.Attitude);
        var existing = await db.StudentEvaluations.FirstOrDefaultAsync(e => e.ClassId == cls.Id && e.StudentId == cmd.StudentId, ct);
        if (existing is null)
        {
            var created = StudentEvaluation.Create(cls.Id, cmd.StudentId, me, scores, cmd.Strengths, cmd.Weaknesses, cmd.Recommendation, cmd.Advice,
                cls.ActualEndDate, clock.Today);
            if (created.IsFailure) return created.Error;
            db.StudentEvaluations.Add(created.Value);
        }
        else if (existing.Update(me, scores, cmd.Strengths, cmd.Weaknesses, cmd.Recommendation, cmd.Advice, cls.ActualEndDate, clock.Today)
                 is { IsFailure: true } r) return r.Error;

        await db.SaveChangesAsync(ct);
        return (await reader.OfClassAsync(cls, clock.Today, ct)).First(e => e.StudentId == cmd.StudentId);
    }
}
