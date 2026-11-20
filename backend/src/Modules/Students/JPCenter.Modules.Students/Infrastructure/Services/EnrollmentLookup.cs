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

    public async Task<IReadOnlyList<StudentEnrollmentEntry>> OfStudentAsync(Guid studentId, CancellationToken ct)
    {
        var sid = new JPCenter.Modules.Students.Domain.Students.StudentId(studentId);
        var rows = await db.Enrollments.AsNoTracking().Where(e => e.StudentId == sid)
            .Select(e => new { e.Id, e.ClassId, e.Status, e.EnrolledAt, e.EndedAt, e.ReservedUntil }).ToListAsync(ct);
        return rows.Select(r => new StudentEnrollmentEntry(r.Id.Value, r.ClassId, SnakeCaseEnumConverter<EnrollmentStatus>.ToText(r.Status),
            r.EnrolledAt, r.EndedAt, r.ReservedUntil)).ToList();
    }

    public async Task<IReadOnlyDictionary<Guid, string>> NamesAsync(IReadOnlyCollection<Guid> studentIds, CancellationToken ct)
    {
        var ids = studentIds.Distinct().Select(x => new JPCenter.Modules.Students.Domain.Students.StudentId(x)).ToList();
        if (ids.Count == 0) return new Dictionary<Guid, string>();
        return await db.Students.AsNoTracking().IgnoreQueryFilters().Where(s => ids.Contains(s.Id))
            .ToDictionaryAsync(s => s.Id.Value, s => s.FullName, ct);
    }

    public async Task<IReadOnlyList<StudentSummary>> SummariesAsync(IReadOnlyCollection<Guid> studentIds, CancellationToken ct)
    {
        var ids = studentIds.Distinct().Select(x => new JPCenter.Modules.Students.Domain.Students.StudentId(x)).ToList();
        if (ids.Count == 0) return [];
        return (await db.Students.AsNoTracking().IgnoreQueryFilters().Where(s => ids.Contains(s.Id)).ToListAsync(ct))
            .Select(s => new StudentSummary(s.Id.Value, s.Code, s.FullName, s.Dob, s.BranchId.Value)).OrderBy(s => s.Code).ToList();
    }
}
