using JPCenter.Application.Common.Messaging;
using JPCenter.Modules.Crm.Application.Abstractions;
using JPCenter.Modules.Crm.Application.Appointments.Common;
using JPCenter.Modules.Crm.Domain.Appointments;
using JPCenter.SharedKernel.Domain;
using JPCenter.SharedKernel.Primitives;

namespace JPCenter.Modules.Crm.Application.Appointments.RescheduleAppointment;

/// <summary>CRM-14 bước 5 — "Đổi lịch": giờ / phòng / người tiếp, kiểm trùng lại, nhắc lại từ đầu; chỉ lịch còn "Đã hẹn"</summary>
public sealed record RescheduleAppointmentCommand(Guid Id, Guid? RoomId, Guid StaffId, DateTimeOffset Start, DateTimeOffset End, string? Note,
    uint Version) : ICommand<AppointmentDto>;

public sealed class RescheduleAppointmentHandler(ICrmDbContext db, AppointmentDesk desk, IClock clock)
    : ICommandHandler<RescheduleAppointmentCommand, AppointmentDto>
{
    public async Task<Result<AppointmentDto>> Handle(RescheduleAppointmentCommand cmd, CancellationToken ct)
    {
        var appt = await desk.ForUpdateAsync(cmd.Id, ct, cmd.Version);
        if (appt is null) return AppointmentErrors.NotFound;
        if (appt.Status != AppointmentStatus.Scheduled) return AppointmentErrors.NotScheduled;
        var conflicts = await desk.ConflictsAsync(appt.Id, appt.LeadId, cmd.RoomId, cmd.StaffId, cmd.Start, cmd.End, ct);
        if (conflicts.Count > 0) return AppointmentDesk.ConflictError(conflicts);
        if (appt.Reschedule(cmd.Start, cmd.End, cmd.RoomId, cmd.StaffId, cmd.Note, clock.UtcNow) is { IsFailure: true } bad) return bad.Error;
        await db.SaveChangesAsync(ct);
        return await desk.ToDtoAsync(appt, ct);
    }
}
