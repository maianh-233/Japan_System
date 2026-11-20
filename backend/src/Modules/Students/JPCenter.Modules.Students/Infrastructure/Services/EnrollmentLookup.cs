using JPCenter.Infrastructure.Common.Persistence.Conversions;
using JPCenter.Modules.Students.Contracts;
using JPCenter.Modules.Students.Contracts.Dtos;
using JPCenter.Modules.Students.Domain.Enrollments;
using JPCenter.Modules.Students.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;

namespace JPCenter.Modules.Students.Infrastructure.Services;

/// <summary>Hiện thực <see cref="IEnrollmentLookup"/> — sĩ số + danh sách lớp cho Academic.</summary>
internal sealed class EnrollmentLookup(StudentsDbContext db) : IEnrollmentLookup
{
    public async Task<IReadOnlyDictionary<Guid, int>> ActiveCountByClassAsync(IReadOnlyCollection<Guid> classIds, CancellationToken ct)
    {
        var ids = classIds.Distinct().ToList();
        if (ids.Count == 0) return new Dictionary<Guid, int>();
        return await db.Enrollments.AsNoTracking()
            .Where(e => ids.Contains(e.ClassId) && (e.Status == EnrollmentStatus.Studying || e.Status == EnrollmentStatus.Reserved))
            .GroupBy(e => e.ClassId)
            .Select(g => new { g.Key, Count = g.Count() })
            .ToDictionaryAsync(x => x.Key, x => x.Count, ct);
    }

    public async Task<IReadOnlyList<RosterEntry>> RosterAsync(Guid classId, CancellationToken ct)
    {
        var rows = await (from e in db.Enrollments.AsNoTracking()
                          join s in db.Students.AsNoTracking() on e.StudentId equals s.Id
                          where e.ClassId == classId && (e.Status == EnrollmentStatus.Studying || e.Status == EnrollmentStatus.Reserved)
                          select new { e, s.Code, s.FullName })
            .ToListAsync(ct);
        return rows
            .Select(r => new RosterEntry(r.e.StudentId.Value, r.e.Id.Value, r.Code, r.FullName, SnakeCaseEnumConverter<EnrollmentStatus>.ToText(r.e.Status),
                r.e.ReservedUntil))
            .OrderBy(r => r.FullName, StringComparer.Create(new global::System.Globalization.CultureInfo("vi-VN"), true))
            .ToList();
    }
}
