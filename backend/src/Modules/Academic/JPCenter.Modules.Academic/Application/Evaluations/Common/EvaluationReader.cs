using JPCenter.Modules.Academic.Application.Abstractions;
using JPCenter.Modules.Academic.Application.Common;
using JPCenter.Modules.Academic.Domain.Classes;
using JPCenter.Modules.Academic.Domain.Reviews;
using Microsoft.EntityFrameworkCore;

namespace JPCenter.Modules.Academic.Application.Evaluations.Common;

/// <summary>1–5 sao từng kỹ năng — khớp <c>skills: Record&lt;EvalSkill, number&gt;</c></summary>
public sealed record EvalSkillsDto(int Vocab, int Grammar, int Reading, int Listening, int Speaking);

/// <summary>
/// Nhận xét cuối khóa — khớp <c>StudentEvaluation</c> ở <c>frontend/src/features/academic/classes/types.ts</c> (<c>author</c> = tên GV).
/// CHỈ trả cho nhân viên và GV của lớp — không bao giờ trả về cổng HV / PH (DT-19.E2).
/// </summary>
public sealed record EvaluationDto(
    Guid Id,
    Guid ClassId,
    Guid StudentId,
    Guid TeacherId,
    string Author,
    EvalSkillsDto Skills,
    int Attitude,
    string? Strengths,
    string? Weaknesses,
    EvalRecommendation Recommendation,
    string? Advice,
    DateTimeOffset CreatedAt,
    DateTimeOffset UpdatedAt,
    bool Editable);

public sealed class EvaluationReader(IAcademicDbContext db, PeopleNames people)
{
    public async Task<IReadOnlyList<EvaluationDto>> OfClassAsync(Class cls, DateOnly today, CancellationToken ct)
    {
        var rows = await db.StudentEvaluations.AsNoTracking().Where(e => e.ClassId == cls.Id)
            .Select(e => new { E = e, Created = EF.Property<DateTimeOffset>(e, "CreatedAt"), Updated = EF.Property<DateTimeOffset?>(e, "UpdatedAt") })
            .ToListAsync(ct);
        var names = await people.OfAsync(rows.Select(r => (Guid?)r.E.TeacherId), ct);
        var editable = StudentEvaluation.IsEditable(cls.ActualEndDate, today);
        return rows.Select(r => new EvaluationDto(r.E.Id.Value, cls.Id.Value, r.E.StudentId, r.E.TeacherId, names.GetValueOrDefault(r.E.TeacherId, "—"),
            new EvalSkillsDto(r.E.Vocab, r.E.Grammar, r.E.Reading, r.E.Listening, r.E.Speaking), r.E.Attitude, r.E.Strengths, r.E.Weaknesses,
            r.E.Recommendation, r.E.Advice, r.Created, r.Updated ?? r.Created, editable)).ToList();
    }
}
