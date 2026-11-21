using System.Text.Json;
using JPCenter.Application.Common.Security;
using JPCenter.Modules.Academic.Contracts;
using JPCenter.Modules.Crm.Application.Abstractions;
using JPCenter.Modules.Crm.Application.Common;
using JPCenter.Modules.Crm.Domain.Appointments;
using JPCenter.Modules.Crm.Domain.Leads;
using JPCenter.Modules.Hr.Contracts;
using JPCenter.SharedKernel.Primitives;
using Microsoft.EntityFrameworkCore;

namespace JPCenter.Modules.Crm.Application.Appointments.Common;

/// <summary>
/// Dùng chung cho lịch hẹn: phạm vi (<c>crm.appointments</c> theo chi nhánh; own = mình là người tiếp), kiểm trùng phòng / buổi học /
/// người tiếp / giờ dạy / lead (domain <see cref="AppointmentConflicts"/> + Academic, Hr), dựng DTO.
/// </summary>
public sealed class AppointmentDesk(ICrmDbContext db, CrmAccess access, IScheduleLookup schedule, IEmployeeDirectory employees, PeopleNames people)
{
    public static readonly TimeSpan Vietnam = TimeSpan.FromHours(7);

    public IQueryable<Appointment> Scope(IQueryable<Appointment> q, PermissionAction action)
    {
        if (access.Branches(CrmAccess.Appointments, action) is { } allowed)
        {
            var ids = allowed.Select(b => new BranchId(b)).ToList();
            q = q.Where(a => ids.Contains(a.BranchId));
        }
        if (access.OwnOnly(CrmAccess.Appointments, action) is { } me) q = q.Where(a => a.StaffId == me);
        return q;
    }

    public async Task<Appointment?> ForUpdateAsync(Guid id, CancellationToken ct, uint version = 0)
    {
        var a = await Scope(db.Appointments, PermissionAction.Update).FirstOrDefaultAsync(x => x.Id == new AppointmentId(id), ct);
        if (a is not null && version != 0) ((DbContext)db).Entry(a).Property("xmin").OriginalValue = version;
        return a;
    }

    /// <summary>CRM-14.E2 → E5 — mọi chỗ trùng của 1 lịch (đặt mới: <paramref name="selfId"/> null)</summary>
    public async Task<IReadOnlyList<AppointmentConflict>> ConflictsAsync(AppointmentId? selfId, LeadId leadId, Guid? roomId, Guid staffId,
        DateTimeOffset start, DateTimeOffset end, CancellationToken ct)
    {
        if (end <= start) return [];
        (start, end) = (start.ToUniversalTime(), end.ToUniversalTime());
        var appts = await db.Appointments.AsNoTracking()
            .Where(a => a.Status == AppointmentStatus.Scheduled && a.Start < end && start < a.End
                && (a.LeadId == leadId || a.StaffId == staffId || (roomId != null && a.RoomId == roomId)))
            .ToListAsync(ct);
        var apptLeads = appts.Select(a => a.LeadId).Distinct().ToList();
        var leadNames = await db.Leads.AsNoTracking().IgnoreQueryFilters().Where(l => apptLeads.Contains(l.Id))
            .ToDictionaryAsync(l => l.Id, l => l.FullName, ct);

        var room = roomId is { } rid ? await schedule.RoomAsync(rid, ct) : null;
        var staff = await employees.FindAsync(staffId, ct);
        var from = DateOnly.FromDateTime(start.ToOffset(Vietnam).DateTime);
        var to = DateOnly.FromDateTime(end.ToOffset(Vietnam).DateTime);
        var busy = await schedule.BusyAsync(from, to, roomId, staff is { IsTeacher: true } ? staffId : null, ct);
        DateTimeOffset At(DateOnly d, TimeOnly t) => new DateTimeOffset(d.ToDateTime(t), Vietnam).ToUniversalTime();

        var roomSessions = busy.Where(b => roomId is not null && b.RoomId == roomId)
            .Select(b => new BusySlot(At(b.Date, b.Start), At(b.Date, b.End), $"lớp {b.ClassCode} học {b.Start:HH\\:mm}–{b.End:HH\\:mm}")).ToList();
        var teaching = busy.Where(b => b.TeacherIds.Contains(staffId))
            .Select(b => new BusySlot(At(b.Date, b.Start), At(b.Date, b.End), $"lớp {b.ClassCode} ({b.Start:HH\\:mm}–{b.End:HH\\:mm})")).ToList();

        var ctx = new ConflictContext(appts, room?.Name, room?.UnavailableLabel, roomSessions, staff?.FullName ?? "Người tiếp",
            staff?.Status == "leave", teaching, id => leadNames.GetValueOrDefault(id) ?? "", t => t.ToOffset(Vietnam).DateTime);
        var found = AppointmentConflicts.Find(selfId, leadId, roomId, staffId, start, end, ctx).ToList();
        if (staff is null or { HasQuit: true }) found.Add(new AppointmentConflict(ConflictKind.Staff, "Người tiếp không còn làm việc"));
        return found;
    }

    /// <summary>Lỗi <c>crm.appointment.conflict</c> kèm <c>errors</c> theo loại trùng (room / staff / lead)</summary>
    public static Error ConflictError(IReadOnlyList<AppointmentConflict> conflicts) => AppointmentErrors.Conflict with
    {
        FieldErrors = conflicts.GroupBy(c => JsonNamingPolicy.CamelCase.ConvertName(c.Kind.ToString()))
            .ToDictionary(g => g.Key, g => g.Select(c => c.Message).ToArray()),
    };

    public async Task<IReadOnlyList<AppointmentDto>> ToDtosAsync(IReadOnlyList<Appointment> list, CancellationToken ct)
    {
        if (list.Count == 0) return [];
        var ids = list.Select(a => a.Id).ToList();
        var meta = await db.Appointments.AsNoTracking().Where(a => ids.Contains(a.Id))
            .Select(a => new { a.Id, Created = EF.Property<DateTimeOffset>(a, "CreatedAt"), Version = EF.Property<uint>(a, "xmin") })
            .ToDictionaryAsync(x => x.Id, ct);
        var leadIds = list.Select(a => a.LeadId).Distinct().ToList();
        var leads = await db.Leads.AsNoTracking().IgnoreQueryFilters().Where(l => leadIds.Contains(l.Id))
            .Select(l => new { l.Id, l.FullName, l.Phone }).ToDictionaryAsync(l => l.Id, ct);
        var rooms = await schedule.RoomsAsync(list.Where(a => a.RoomId != null).Select(a => a.RoomId!.Value).Distinct().ToList(), ct);
        var names = await people.OfAsync(list.Select(a => (Guid?)a.StaffId), ct);
        return list.Select(a =>
        {
            var m = meta.GetValueOrDefault(a.Id);
            var lead = leads.GetValueOrDefault(a.LeadId);
            return new AppointmentDto(a.Id.Value, a.Code, a.LeadId.Value, lead?.FullName ?? "—", lead?.Phone ?? "", a.Kind, a.BranchId.Value, a.RoomId,
                a.RoomId is { } r ? rooms.GetValueOrDefault(r)?.Name : null, a.StaffId, names.GetValueOrDefault(a.StaffId) ?? "—", a.Start, a.End,
                a.Status, a.Note, a.Outcome, m?.Created ?? default, a.RemindedAt, a.CancelReason, m?.Version ?? 0);
        }).ToList();
    }

    public async Task<AppointmentDto> ToDtoAsync(Appointment a, CancellationToken ct) => (await ToDtosAsync([a], ct))[0];
}
