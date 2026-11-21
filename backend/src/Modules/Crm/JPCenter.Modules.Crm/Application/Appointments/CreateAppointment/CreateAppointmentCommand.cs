using System.Globalization;
using JPCenter.Application.Common.Messaging;
using JPCenter.Application.Common.Security;
using JPCenter.Modules.Crm.Application.Abstractions;
using JPCenter.Modules.Crm.Application.Appointments.Common;
using JPCenter.Modules.Crm.Application.Common;
using JPCenter.Modules.Crm.Domain.Appointments;
using JPCenter.Modules.Crm.Domain.Leads;
using JPCenter.SharedKernel.Domain;
using JPCenter.SharedKernel.Primitives;
using Microsoft.EntityFrameworkCore;

namespace JPCenter.Modules.Crm.Application.Appointments.CreateAppointment;

/// <summary>
/// CRM-14 — đặt lịch hẹn tư vấn / test tại trung tâm: giờ hợp lệ (E1), không trùng phòng / buổi học / người tiếp / lead (E2 → E6),
/// mã LH0001, hẹn test → lead "Hẹn test", dòng hệ thống ở lịch sử tư vấn. <paramref name="End"/> trống = thời lượng mặc định theo loại.
/// </summary>
public sealed record CreateAppointmentCommand(Guid LeadId, AppointmentKind Kind, Guid? BranchId, Guid? RoomId, Guid StaffId, DateTimeOffset Start,
    DateTimeOffset? End, string? Note) : ICommand<AppointmentDto>;

public sealed class CreateAppointmentHandler(ICrmDbContext db, CrmAccess access, AppointmentDesk desk, IClock clock)
    : ICommandHandler<CreateAppointmentCommand, AppointmentDto>
{
    public async Task<Result<AppointmentDto>> Handle(CreateAppointmentCommand cmd, CancellationToken ct)
    {
        var lead = await db.Leads.FirstOrDefaultAsync(l => l.Id == new LeadId(cmd.LeadId), ct);
        if (lead is null) return LeadErrors.NotFound;
        var branch = cmd.BranchId ?? lead.BranchId.Value;
        if (!access.InBranch(CrmAccess.Appointments, PermissionAction.Create, branch)) return CrmAccess.BranchNotAllowed;

        var end = cmd.End ?? cmd.Start.AddMinutes(Appointment.DefaultDuration(cmd.Kind));
        var now = clock.UtcNow;
        var created = Appointment.Create(await NextCodeAsync(ct), lead.Id, cmd.Kind, new BranchId(branch), cmd.RoomId, cmd.StaffId, cmd.Start, end,
            cmd.Note, now);
        if (created.IsFailure) return created.Error;
        var conflicts = await desk.ConflictsAsync(null, lead.Id, cmd.RoomId, cmd.StaffId, cmd.Start, end, ct);
        if (conflicts.Count > 0) return AppointmentDesk.ConflictError(conflicts);

        var appt = created.Value;
        db.Appointments.Add(appt);
        if (cmd.Kind == AppointmentKind.Test) lead.MarkTestScheduled();
        lead.AddSystemNote($"Đặt lịch {(cmd.Kind == AppointmentKind.Test ? "test" : "tư vấn")} tại trung tâm {appt.Code} lúc "
            + $"{cmd.Start.ToOffset(AppointmentDesk.Vietnam):HH:mm dd/MM}", now);
        await db.SaveChangesAsync(ct);
        return await desk.ToDtoAsync(appt, ct);
    }

    private async Task<string> NextCodeAsync(CancellationToken ct)
    {
        var codes = await db.Appointments.Select(a => a.Code).ToListAsync(ct);
        var max = codes.Select(c => c.StartsWith("LH", StringComparison.Ordinal)
            && int.TryParse(c[2..], NumberStyles.None, CultureInfo.InvariantCulture, out var n) ? n : 0).DefaultIfEmpty(0).Max();
        return Appointment.CodeFor(max + 1);
    }
}
