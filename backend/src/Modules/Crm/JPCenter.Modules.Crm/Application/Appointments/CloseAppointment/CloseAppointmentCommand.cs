using JPCenter.Application.Common.Messaging;
using JPCenter.Modules.Crm.Application.Abstractions;
using JPCenter.Modules.Crm.Application.Appointments.Common;
using JPCenter.Modules.Crm.Application.Common;
using JPCenter.Modules.Crm.Domain.Appointments;
using JPCenter.Modules.Crm.Domain.Leads;
using JPCenter.SharedKernel.Domain;
using JPCenter.SharedKernel.Primitives;
using Microsoft.EntityFrameworkCore;

namespace JPCenter.Modules.Crm.Application.Appointments.CloseAppointment;

/// <summary>Kết quả lịch hẹn (CRM-15)</summary>
public enum AppointmentOutcome
{
    /// <summary>Đã đến — bắt buộc kết quả buổi hẹn, thêm dòng tư vấn kênh "gặp"</summary>
    Done,
    /// <summary>Không đến — chỉ sau giờ bắt đầu (E2), lead hẹn gọi lại sau 1 ngày</summary>
    NoShow,
    /// <summary>Hủy lịch — bắt buộc lý do</summary>
    Cancel,
}

/// <summary>CRM-15 — ghi kết quả lịch hẹn còn "Đã hẹn" (E1)</summary>
public sealed record CloseAppointmentCommand(Guid Id, AppointmentOutcome Outcome, string? Text) : ICommand<AppointmentDto>;

public sealed class CloseAppointmentHandler(ICrmDbContext db, CrmAccess access, AppointmentDesk desk, IClock clock)
    : ICommandHandler<CloseAppointmentCommand, AppointmentDto>
{
    public async Task<Result<AppointmentDto>> Handle(CloseAppointmentCommand cmd, CancellationToken ct)
    {
        var appt = await desk.ForUpdateAsync(cmd.Id, ct);
        if (appt is null) return AppointmentErrors.NotFound;
        var now = clock.UtcNow;
        var lead = await db.Leads.FirstAsync(l => l.Id == appt.LeadId, ct);
        var kind = appt.Kind == AppointmentKind.Test ? "test" : "tư vấn";

        var done = cmd.Outcome switch
        {
            AppointmentOutcome.Done => appt.MarkDone(cmd.Text ?? ""),
            AppointmentOutcome.NoShow => appt.MarkNoShow(now),
            _ => appt.Cancel(cmd.Text ?? ""),
        };
        if (done.IsFailure) return done.Error;

        switch (cmd.Outcome)
        {
            case AppointmentOutcome.Done:
                lead.AddConsultation(ConsultChannel.Meeting, appt.Kind == AppointmentKind.Test ? ConsultResult.TestBooked : ConsultResult.Interested,
                    $"Đến {kind} tại trung tâm ({appt.Code}): {appt.Outcome}", access.ActorId, access.ActorName, now, null);
                break;
            case AppointmentOutcome.NoShow:
                lead.ScheduleFollowUp(now.AddDays(1));
                lead.AddSystemNote($"Không đến lịch {kind} {appt.Code} — hẹn gọi lại sau 1 ngày", now);
                break;
            default:
                lead.AddSystemNote($"Hủy lịch {kind} {appt.Code}: {appt.CancelReason}", now);
                break;
        }
        await db.SaveChangesAsync(ct);
        return await desk.ToDtoAsync(appt, ct);
    }
}
