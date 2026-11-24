using JPCenter.Infrastructure.Common.Persistence.Conversions;
using JPCenter.Modules.Academic.Application.Common;
using JPCenter.Modules.Academic.Contracts;
using JPCenter.Modules.Academic.Contracts.Dtos.Reports;
using JPCenter.Modules.Academic.Domain.Classes;
using JPCenter.Modules.Academic.Domain.ClassSessions;
using JPCenter.Modules.Academic.Domain.Requests;
using JPCenter.Modules.Academic.Infrastructure.Persistence;
using JPCenter.SharedKernel.Primitives;
using Microsoft.EntityFrameworkCore;

namespace JPCenter.Modules.Academic.Infrastructure.Services;

/// <summary>Hiện thực <see cref="IAcademicReportSource"/> — chiếu cột cần dùng, gom theo tháng / phòng trong bộ nhớ (vài nghìn dòng).</summary>
internal sealed class AcademicReportSource(AcademicDbContext db, PeopleNames people) : IAcademicReportSource
{
    public async Task<IReadOnlyList<ClassFact>> OpenClassesAsync(IReadOnlyCollection<Guid>? branchIds, CancellationToken ct)
    {
        var ids = BranchIds(branchIds);
        var query = db.Classes.AsNoTracking().Where(c => c.Status == ClassStatus.Enrolling || c.Status == ClassStatus.Ongoing);
        if (ids is not null) query = query.Where(c => ids.Contains(c.BranchId));
        var classes = await query.Select(c => new { c.Id, c.Code, c.BranchId, c.Status, c.MaxStudents, c.CourseId }).ToListAsync(ct);
        var courseIds = classes.Select(c => c.CourseId).Distinct().ToList();
        var courses = await db.Courses.AsNoTracking().IgnoreQueryFilters().Where(c => courseIds.Contains(c.Id))
            .ToDictionaryAsync(c => c.Id, c => c.Name, ct);
        return classes.OrderBy(c => c.Code, StringComparer.Ordinal)
            .Select(c => new ClassFact(c.Id.Value, c.Code, c.BranchId.Value, SnakeCaseEnumConverter<ClassStatus>.ToText(c.Status), c.MaxStudents,
                courses.GetValueOrDefault(c.CourseId) ?? string.Empty))
            .ToList();
    }

    public async Task<IReadOnlyList<AttendanceMonthFact>> AttendanceByMonthAsync(IReadOnlyCollection<Guid>? branchIds, DateOnly from, DateOnly to,
        CancellationToken ct)
    {
        var rows = await Sessions(branchIds)
            .Where(s => s.Status != SessionStatus.Cancelled && s.Date >= from && s.Date <= to)
            .SelectMany(s => s.Attendance.Select(a => new { s.BranchId, s.Date, a.Status }))
            .ToListAsync(ct);
        return rows.GroupBy(r => (r.BranchId, Month: new DateOnly(r.Date.Year, r.Date.Month, 1)))
            .Select(g => new AttendanceMonthFact(g.Key.BranchId.Value, g.Key.Month,
                g.Count(r => r.Status is AttendanceStatus.Present or AttendanceStatus.Late), g.Count()))
            .OrderBy(f => f.Month).ToList();
    }

    public async Task<IReadOnlyList<RoomUsageFact>> RoomUsageAsync(IReadOnlyCollection<Guid>? branchIds, DateOnly from, DateOnly to, CancellationToken ct)
    {
        var ids = BranchIds(branchIds);
        var roomQuery = db.Rooms.AsNoTracking();
        if (ids is not null) roomQuery = roomQuery.Where(r => ids.Contains(r.BranchId));
        var rooms = await roomQuery.Select(r => new { r.Id, r.Name, r.BranchId }).ToListAsync(ct);

        var sessions = await Sessions(branchIds)
            .Where(s => s.Status != SessionStatus.Cancelled && s.Date >= from && s.Date <= to)
            .Select(s => new { s.ClassId, s.RoomId, s.StartTime, s.EndTime })
            .ToListAsync(ct);
        var classIds = sessions.Select(s => s.ClassId).Distinct().ToList();
        var classes = await db.Classes.AsNoTracking().IgnoreQueryFilters().Where(c => classIds.Contains(c.Id))
            .ToDictionaryAsync(c => c.Id, c => new { c.RoomId, c.Online }, ct);

        var minutes = sessions
            .Select(s => (Class: classes.GetValueOrDefault(s.ClassId), s.RoomId, Minutes: (int)(s.EndTime - s.StartTime).TotalMinutes))
            .Where(s => s.Class is { Online: false })
            .Select(s => (Room: s.RoomId ?? s.Class!.RoomId, s.Minutes))
            .Where(s => s.Room is not null)
            .GroupBy(s => s.Room!.Value)
            .ToDictionary(g => g.Key, g => g.Sum(s => s.Minutes));
        return rooms.Select(r => new RoomUsageFact(r.Id.Value, r.Name, r.BranchId.Value, minutes.GetValueOrDefault(r.Id))).ToList();
    }

    public async Task<SessionAlertCounts> SessionAlertsAsync(IReadOnlyCollection<Guid>? branchIds, DateOnly unconfirmedFrom, DateOnly missingLogFrom,
        CancellationToken ct)
    {
        var from = unconfirmedFrom < missingLogFrom ? unconfirmedFrom : missingLogFrom;
        var done = await Sessions(branchIds).Where(s => s.Status == SessionStatus.Completed && s.Date >= from).ToListAsync(ct);
        return new SessionAlertCounts(
            done.Count(s => s.Date >= unconfirmedFrom && s.ConfirmedAt is null),
            done.Count(s => s.Date >= missingLogFrom && s.Report is null));
    }

    public async Task<IReadOnlyList<SessionWithoutAttendance>> SessionsWithoutAttendanceAsync(IReadOnlyCollection<Guid>? branchIds, DateOnly from,
        DateOnly to, CancellationToken ct)
    {
        var sessions = await Sessions(branchIds)
            .Where(s => s.Status != SessionStatus.Cancelled && s.AttendanceAt == null && s.Date >= from && s.Date <= to)
            .Select(s => new { s.Id, s.ClassId, s.Date, s.StartTime, s.EndTime, s.Kind })
            .ToListAsync(ct);
        var classIds = sessions.Select(s => s.ClassId).Distinct().ToList();
        var classes = await db.Classes.AsNoTracking().Where(c => classIds.Contains(c.Id) && c.Status != ClassStatus.Cancelled)
            .ToDictionaryAsync(c => c.Id, c => c.Code, ct);
        return sessions.Where(s => classes.ContainsKey(s.ClassId))
            .Select(s => new SessionWithoutAttendance(s.Id.Value, s.ClassId.Value, classes[s.ClassId], s.Date, s.StartTime, s.EndTime,
                s.Kind == SessionKind.Exam))
            .ToList();
    }

    public async Task<IReadOnlyList<PendingClassRequest>> PendingRequestsAsync(IReadOnlyCollection<Guid>? branchIds, CancellationToken ct)
    {
        var ids = BranchIds(branchIds);
        var cancels = db.SessionCancelRequests.AsNoTracking().Where(r => r.Status == RequestStatus.Pending);
        var leaves = db.LeaveRequests.AsNoTracking().Where(r => r.Status == RequestStatus.Pending);
        if (ids is not null)
        {
            cancels = cancels.Where(r => ids.Contains(r.BranchId));
            leaves = leaves.Where(r => ids.Contains(r.BranchId));
        }
        var c = await cancels.Select(r => new { r.Id, r.TeacherId, r.BranchId, r.ClassId, r.SessionId, r.RequestedAt }).ToListAsync(ct);
        var l = await leaves.Select(r => new { r.Id, r.ByName, r.BranchId, r.ClassId, r.SessionId, r.RequestedAt }).ToListAsync(ct);

        var sessionIds = c.Select(r => r.SessionId).Concat(l.Select(r => r.SessionId)).Distinct().ToList();
        var sessions = await db.ClassSessions.AsNoTracking().Where(s => sessionIds.Contains(s.Id))
            .ToDictionaryAsync(s => s.Id, s => s.Date.ToDateTime(s.StartTime), ct);
        var classIds = c.Select(r => r.ClassId).Concat(l.Select(r => r.ClassId)).Distinct().ToList();
        var classes = await db.Classes.AsNoTracking().IgnoreQueryFilters().Where(k => classIds.Contains(k.Id))
            .ToDictionaryAsync(k => k.Id, k => k.Code, ct);
        var teachers = await people.OfAsync(c.Select(r => (Guid?)r.TeacherId), ct);

        DateTime? StartOf(ClassSessionId id) => sessions.TryGetValue(id, out var at) ? at : null;
        return c.Select(r => new PendingClassRequest(r.Id.Value, "cancel", teachers.GetValueOrDefault(r.TeacherId) ?? "Giáo viên", r.BranchId.Value,
                classes.GetValueOrDefault(r.ClassId), r.RequestedAt, StartOf(r.SessionId)))
            .Concat(l.Select(r => new PendingClassRequest(r.Id.Value, "leave", r.ByName, r.BranchId.Value, classes.GetValueOrDefault(r.ClassId),
                r.RequestedAt, StartOf(r.SessionId))))
            .OrderBy(r => r.RequestedAt)
            .ToList();
    }

    private IQueryable<ClassSession> Sessions(IReadOnlyCollection<Guid>? branchIds)
    {
        var query = db.ClassSessions.AsNoTracking();
        var ids = BranchIds(branchIds);
        return ids is null ? query : query.Where(s => ids.Contains(s.BranchId));
    }

    private static List<BranchId>? BranchIds(IReadOnlyCollection<Guid>? branchIds) => branchIds?.Select(b => new BranchId(b)).ToList();
}
