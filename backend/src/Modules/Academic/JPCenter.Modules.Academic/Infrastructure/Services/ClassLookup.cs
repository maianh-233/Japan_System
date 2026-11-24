using JPCenter.Infrastructure.Common.Persistence.Conversions;
using JPCenter.Modules.Academic.Contracts;
using JPCenter.Modules.Academic.Contracts.Dtos;
using JPCenter.Modules.Academic.Domain.Classes;
using JPCenter.Modules.Academic.Domain.ClassSessions;
using JPCenter.Modules.Academic.Infrastructure.Persistence;
using JPCenter.SharedKernel.Primitives;
using Microsoft.EntityFrameworkCore;

namespace JPCenter.Modules.Academic.Infrastructure.Services;

/// <summary>Hiện thực <see cref="IClassLookup"/> cho module khác đọc lớp (Students ghi danh, Finance hóa đơn…).</summary>
internal sealed class ClassLookup(AcademicDbContext db) : IClassLookup
{
    public async Task<ClassSummary?> FindAsync(Guid classId, CancellationToken ct) =>
        (await FindManyAsync([classId], ct)).GetValueOrDefault(classId);

    public async Task<IReadOnlyDictionary<Guid, ClassSummary>> FindManyAsync(IReadOnlyCollection<Guid> classIds, CancellationToken ct)
    {
        var ids = classIds.Distinct().Select(x => new ClassId(x)).ToList();
        if (ids.Count == 0) return new Dictionary<Guid, ClassSummary>();
        var classes = await db.Classes.AsNoTracking().Where(c => ids.Contains(c.Id)).ToListAsync(ct);
        return await SummariesAsync(classes, ct);
    }

    public async Task<IReadOnlyList<ClassSummary>> TaughtByAsync(Guid teacherId, CancellationToken ct)
    {
        var classes = (await db.Classes.AsNoTracking().Where(c => c.Status != ClassStatus.Cancelled).ToListAsync(ct))
            .Where(c => c.TeacherIds.Contains(teacherId) || c.AssistantIds.Contains(teacherId)).ToList();
        return (await SummariesAsync(classes, ct)).Values.OrderBy(c => c.Code).ToList();
    }

    private async Task<IReadOnlyDictionary<Guid, ClassSummary>> SummariesAsync(IReadOnlyList<Class> classes, CancellationToken ct)
    {
        if (classes.Count == 0) return new Dictionary<Guid, ClassSummary>();
        var ids = classes.Select(c => c.Id).ToList();
        var taught = await db.ClassSessions.AsNoTracking()
            .Where(s => ids.Contains(s.ClassId) && s.Origin != SessionOrigin.Extra && s.ConfirmedAt != null)
            .GroupBy(s => s.ClassId)
            .Select(g => new { g.Key, Count = g.Count() })
            .ToDictionaryAsync(x => x.Key, x => x.Count, ct);
        var courseIds = classes.Select(c => c.CourseId).Distinct().ToList();
        var levels = await db.Courses.AsNoTracking().IgnoreQueryFilters().Where(c => courseIds.Contains(c.Id))
            .ToDictionaryAsync(c => c.Id, c => c.LevelTo, ct);
        return classes.ToDictionary(c => c.Id.Value, c => new ClassSummary(c.Id.Value, c.Code, c.Name, c.BranchId.Value, c.CourseId.Value,
            SnakeCaseEnumConverter<ClassStatus>.ToText(c.Status), c.MaxStudents, c.Fee.Amount, c.SessionCount, taught.GetValueOrDefault(c.Id),
            levels.TryGetValue(c.CourseId, out var level) ? JlptLevelText.Of(level) : null));
    }
}
