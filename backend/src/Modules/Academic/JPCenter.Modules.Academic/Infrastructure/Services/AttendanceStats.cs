using JPCenter.Modules.Academic.Application.ClassSessions.Common;
using JPCenter.Modules.Academic.Contracts;
using JPCenter.Modules.Academic.Contracts.Dtos;
using JPCenter.Modules.Academic.Domain.Classes;
using JPCenter.Modules.Academic.Domain.ClassSessions;
using JPCenter.Modules.Academic.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;

namespace JPCenter.Modules.Academic.Infrastructure.Services;

/// <summary>Hiện thực <see cref="IAttendanceStats"/> — chuyên cần HV × lớp cho Students (HV-14)</summary>
internal sealed class AttendanceStats(AcademicDbContext db) : IAttendanceStats
{
    public async Task<IReadOnlyList<AttendanceStat>> ForClassesAsync(IReadOnlyCollection<Guid> classIds, IReadOnlyCollection<Guid>? studentIds,
        CancellationToken ct)
    {
        var ids = classIds.Distinct().Select(c => new ClassId(c)).ToList();
        if (ids.Count == 0) return [];
        var sessions = await db.ClassSessions.AsNoTracking().IgnoreQueryFilters()
            .Where(s => ids.Contains(s.ClassId) && s.Status != SessionStatus.Cancelled)
            .ToListAsync(ct);

        var result = new List<AttendanceStat>();
        foreach (var group in sessions.GroupBy(s => s.ClassId))
        {
            var students = studentIds ?? group.SelectMany(s => s.Attendance.Where(a => !a.IsGuest).Select(a => a.StudentId)).Distinct().ToList();
            result.AddRange(students.Select(st => AttendanceStatsCalculator.Compute(group.Key.Value, st, group)));
        }
        return result;
    }
}
