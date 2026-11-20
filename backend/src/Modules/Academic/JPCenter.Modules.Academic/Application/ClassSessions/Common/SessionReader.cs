using JPCenter.Application.Common.Security;
using JPCenter.Modules.Academic.Application.Abstractions;
using JPCenter.Modules.Academic.Application.Common;
using JPCenter.Modules.Academic.Domain.Classes;
using JPCenter.Modules.Academic.Domain.ClassSessions;
using JPCenter.Modules.Academic.Domain.Rooms;
using Microsoft.EntityFrameworkCore;

namespace JPCenter.Modules.Academic.Application.ClassSessions.Common;

/// <summary>Bộ lọc buổi học (DT-20): khoảng ngày, chi nhánh, lớp, GV (hiệu lực), phòng (hiệu lực)</summary>
public sealed record SessionFilter(DateOnly? From, DateOnly? To, Guid? BranchId, Guid? ClassId, Guid? TeacherId, Guid? RoomId);

/// <summary>
/// Đọc buổi học ra DTO theo phạm vi của trang (<c>academic.sessions</c> hoặc <c>academic.classes</c> khi xem trong chi tiết lớp):
/// chi nhánh + phạm vi own = buổi mình dạy / trợ giảng / dạy thay / có chấm công.
/// </summary>
public sealed class SessionReader(IAcademicDbContext db, AcademicAccess access, PeopleNames people)
{
    public async Task<IReadOnlyList<SessionDto>> ListAsync(SessionFilter f, string page, CancellationToken ct)
    {
        var query = db.ClassSessions.AsNoTracking();
        if (f.From is { } from) query = query.Where(s => s.Date >= from);
        if (f.To is { } to) query = query.Where(s => s.Date <= to);
        if (f.ClassId is { } cid) query = query.Where(s => s.ClassId == new ClassId(cid));
        if (f.BranchId is { } b) query = query.Where(s => s.BranchId == new SharedKernel.Primitives.BranchId(b));
        var rows = await Rows(query).ToListAsync(ct);
        var ctx = await ContextAsync(rows.Select(r => r.Session).ToList(), ct);
        var list = rows.Where(r => Visible(r.Session, ctx.Classes[r.Session.ClassId], page, PermissionAction.View))
            .Where(r => f.TeacherId is not { } t || Teachers(r.Session, ctx.Classes[r.Session.ClassId]).Contains(t)
                || (r.Session.AssistantIds ?? ctx.Classes[r.Session.ClassId].AssistantIds).Contains(t))
            .Where(r => f.RoomId is not { } room || (r.Session.RoomId ?? ctx.Classes[r.Session.ClassId].RoomId)?.Value == room)
            .ToList();
        return await ToDtosAsync(list, ctx, ct);
    }

    public async Task<SessionDto?> FindAsync(Guid id, string page, CancellationToken ct)
    {
        var row = await Rows(db.ClassSessions.AsNoTracking().Where(s => s.Id == new ClassSessionId(id))).FirstOrDefaultAsync(ct);
        if (row is null) return null;
        var ctx = await ContextAsync([row.Session], ct);
        if (!Visible(row.Session, ctx.Classes[row.Session.ClassId], page, PermissionAction.View)) return null;
        return (await ToDtosAsync([row], ctx, ct))[0];
    }

    public async Task<SessionDto> ReloadAsync(ClassSessionId id, CancellationToken ct)
    {
        var row = await Rows(db.ClassSessions.AsNoTracking().Where(s => s.Id == id)).FirstAsync(ct);
        return (await ToDtosAsync([row], await ContextAsync([row.Session], ct), ct))[0];
    }

    /// <summary>Buổi được theo dõi để sửa + lớp của nó, trong phạm vi <paramref name="action"/> của trang buổi học</summary>
    public async Task<(ClassSession Session, Class Class)?> ForUpdateAsync(Guid id, PermissionAction action, CancellationToken ct)
    {
        var s = await db.ClassSessions.FirstOrDefaultAsync(x => x.Id == new ClassSessionId(id), ct);
        if (s is null) return null;
        var c = await db.Classes.IgnoreQueryFilters().FirstAsync(x => x.Id == s.ClassId, ct);
        return Visible(s, c, AcademicAccess.Sessions, action) ? (s, c) : null;
    }

    public static IReadOnlyList<Guid> Teachers(ClassSession s, Class c) => s.TeacherIds.Count > 0 ? s.TeacherIds : c.TeacherIds;

    public bool Visible(ClassSession s, Class c, string page, PermissionAction action)
    {
        if (!access.InBranch(page, action, s.BranchId.Value)) return false;
        if (access.OwnOnly(page, action) is not { } me) return true;
        return Teachers(s, c).Contains(me) || (s.AssistantIds ?? c.AssistantIds).Contains(me) || s.TeacherAttendance.Any(t => t.TeacherId == me);
    }

    private static IQueryable<Row> Rows(IQueryable<ClassSession> q) =>
        q.AsSplitQuery()
            .Select(s => new Row(s, EF.Property<uint>(s, "xmin")));

    private async Task<Context> ContextAsync(IReadOnlyList<ClassSession> sessions, CancellationToken ct)
    {
        var classIds = sessions.Select(s => s.ClassId).Distinct().ToList();
        var classes = await db.Classes.AsNoTracking().IgnoreQueryFilters().Where(c => classIds.Contains(c.Id)).ToDictionaryAsync(c => c.Id, ct);
        var rooms = await db.Rooms.AsNoTracking().IgnoreQueryFilters().ToDictionaryAsync(r => r.Id, r => r.Name, ct);
        return new Context(classes, rooms);
    }

    private async Task<IReadOnlyList<SessionDto>> ToDtosAsync(IReadOnlyList<Row> rows, Context ctx, CancellationToken ct)
    {
        if (rows.Count == 0) return [];
        var sessions = rows.Select(r => r.Session).ToList();
        var who = await people.OfAsync(sessions.SelectMany(s => new[]
        {
            s.AttendanceBy, s.ConfirmedBy, s.CancelledBy, s.Incident?.ReportedBy, s.Incident?.OutcomeBy, s.Report?.AuthorId,
        }.Concat(s.SupportLog.Select(l => (Guid?)l.By))), ct);

        // Buổi khác được nhắc tới (học bù ở lớp khác) → mã lớp
        var refIds = sessions.SelectMany(s => s.Attendance.Select(a => a.MadeUpSessionId).Concat(s.Attendance.Select(a => a.MakeupForSessionId)))
            .Where(x => x is not null).Select(x => x!.Value).Distinct().ToList();
        var refs = refIds.Count == 0 ? new Dictionary<ClassSessionId, (ClassId ClassId, string Code)>()
            : (await (from s in db.ClassSessions.AsNoTracking()
                      join c in db.Classes.AsNoTracking().IgnoreQueryFilters() on s.ClassId equals c.Id
                      where refIds.Contains(s.Id)
                      select new { s.Id, s.ClassId, c.Code }).ToListAsync(ct))
                .ToDictionary(x => x.Id, x => (x.ClassId, x.Code));

        string Name(Guid? id) => id is { } g && who.TryGetValue(g, out var n) ? n : "—";

        return rows.Select(r =>
        {
            var s = r.Session;
            var c = ctx.Classes[s.ClassId];
            var members = s.Attendance.Where(a => !a.IsGuest).ToList();
            var present = members.Count(a => a.Status is AttendanceStatus.Present or AttendanceStatus.Late || a.MadeUpSessionId is not null);
            return new SessionDto(
                s.Id.Value, c.Id.Value, c.Code, c.Name, s.BranchId.Value, s.No, s.Date, TimeText.Of(s.StartTime), TimeText.Of(s.EndTime), s.Kind,
                s.LessonId?.Value, s.ExamId?.Value, s.Status, s.Origin, s.MakeupForId?.Value,
                s.TeacherIds.Count > 0 ? s.TeacherIds : null, s.AssistantIds, s.RoomId?.Value,
                s.RoomId is { } room ? ctx.Rooms.GetValueOrDefault(room) : null,
                Teachers(s, c), (s.RoomId ?? c.RoomId) is { } er ? ctx.Rooms.GetValueOrDefault(er) : null, c.Online, s.Note,
                s.Attendance.Count == 0 ? null : new AttendanceSummaryDto(present, members.Count),
                s.Attendance.Select(a => new AttendanceRecordDto(a.StudentId, a.Status, a.Note, TimeText.Of(a.CheckInAt), a.Method,
                    TimeText.Of(a.LeftAt), a.LeaveReason, a.LeaveRequestId,
                    a.MadeUpSessionId is { } mu ? new MadeUpDto(mu.Value, refs.TryGetValue(mu, out var m) ? m.Code : null, a.MadeUpStatus) : null,
                    a.MakeupForSessionId is { } mf
                        ? new MakeupForDto(mf.Value, refs.TryGetValue(mf, out var o) ? o.ClassId.Value : null, a.LeaveRequestId)
                        : null)).ToList(),
                s.AttendanceAt is { } aat ? new PersonAtDto(s.AttendanceBy, Name(s.AttendanceBy), aat) : null,
                s.TeacherAttendance.Select(t => new TeacherAttendanceDto(t.TeacherId, t.Role, t.Source, t.CheckedAt, t.Status,
                    TimeText.Of(t.CheckIn), TimeText.Of(t.CheckOut), t.SubstituteId, t.Note)).ToList(),
                s.ConfirmedAt is { } cat ? new ConfirmationDto(s.ConfirmedBy, Name(s.ConfirmedBy), cat, s.ConfirmNote) : null,
                s.Incident is { } i
                    ? new IncidentDto(TimeText.Of(i.At), i.Reason, i.Detail, i.Action, i.TakeoverId, i.Notified,
                        i.Outcome is { } oc ? new IncidentOutcomeDto(oc, i.OutcomeAt is { } oat ? TimeText.Of(oat) : null, i.OutcomeTeacherId, Name(i.OutcomeBy)) : null,
                        i.ReportedBy, Name(i.ReportedBy), i.ReportedAt)
                    : null,
                s.SupportLog.OrderBy(l => l.At).Select(l => new SupportEntryDto(l.Id, l.Kind, l.Note, l.By, Name(l.By), l.At)).ToList(),
                s.Report is { } rep
                    ? new TeacherLogDto(rep.Content, rep.Homework, rep.Issues, rep.Coverage, rep.CoveredChapterIds.Select(x => x.Value).ToList(),
                        rep.EndedAt, rep.AuthorId, Name(rep.AuthorId), rep.UpdatedAt)
                    : null,
                s.RescheduledFromDate is { } rd
                    ? new RescheduledFromDto(rd, TimeText.Of(s.RescheduledFromStart!.Value), TimeText.Of(s.RescheduledFromEnd!.Value), s.RescheduleReason)
                    : null,
                s.CancelledAt is { } cAt ? new PersonAtDto(s.CancelledBy, Name(s.CancelledBy), cAt) : null,
                r.Version);
        }).OrderBy(d => d.Date).ThenBy(d => d.StartTime, StringComparer.Ordinal).ThenBy(d => d.ClassCode).ThenBy(d => d.No).ToList();
    }

    private sealed record Row(ClassSession Session, uint Version);

    private sealed record Context(IReadOnlyDictionary<ClassId, Class> Classes, IReadOnlyDictionary<RoomId, string> Rooms);
}
