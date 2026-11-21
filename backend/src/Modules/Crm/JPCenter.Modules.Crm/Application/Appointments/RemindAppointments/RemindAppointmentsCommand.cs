using JPCenter.Application.Common.Messaging;
using JPCenter.Modules.Crm.Application.Abstractions;
using JPCenter.Modules.Crm.Application.Common;
using JPCenter.Modules.Crm.Domain.Appointments;
using JPCenter.Modules.Crm.Domain.Messages;
using JPCenter.SharedKernel.Domain;
using JPCenter.SharedKernel.Primitives;
using Microsoft.EntityFrameworkCore;

namespace JPCenter.Modules.Crm.Application.Appointments.RemindAppointments;

/// <summary>
/// CRM-14 bước 4 — job 5 phút: lịch "Đã hẹn" bắt đầu trong 60 phút, chưa nhắc → thông báo người tiếp + ghi tin nhắc lead theo mẫu
/// <c>consult_reminder</c> / <c>test_reminder</c> (tin tự động, chưa gửi được tới khi có Tích hợp), đặt mốc đã nhắc. Trả số lịch đã nhắc.
/// </summary>
public sealed record RemindAppointmentsCommand : ICommand<int>;

public sealed class RemindAppointmentsHandler(ICrmDbContext db, CrmNotices notices, IClock clock) : ICommandHandler<RemindAppointmentsCommand, int>
{
    public async Task<Result<int>> Handle(RemindAppointmentsCommand cmd, CancellationToken ct)
    {
        var now = clock.UtcNow;
        var until = now.AddMinutes(Appointment.RemindBeforeMinutes);
        var due = (await db.Appointments.Where(a => a.Status == AppointmentStatus.Scheduled && a.RemindedAt == null && a.Start > now && a.Start <= until)
            .ToListAsync(ct)).Where(a => a.NeedsReminder(now)).ToList();
        if (due.Count == 0) return 0;

        var leadIds = due.Select(a => a.LeadId).Distinct().ToList();
        var leads = await db.Leads.IgnoreQueryFilters().Where(l => leadIds.Contains(l.Id)).ToDictionaryAsync(l => l.Id, ct);
        var templates = await db.MessageTemplates.AsNoTracking()
            .Where(t => t.Active && (t.Scenario == MessageScenario.ConsultReminder || t.Scenario == MessageScenario.TestReminder))
            .ToListAsync(ct);

        foreach (var a in due)
        {
            var lead = leads[a.LeadId];
            var local = a.Start.ToOffset(TimeSpan.FromHours(7));
            var scenario = a.Kind == AppointmentKind.Test ? MessageScenario.TestReminder : MessageScenario.ConsultReminder;
            var template = templates.FirstOrDefault(t => t.Scenario == scenario);
            var body = template?.Render(new Dictionary<string, string?>
            {
                ["ten"] = lead.FullName.Split(' ', StringSplitOptions.RemoveEmptyEntries).LastOrDefault(), ["gio"] = local.ToString("HH:mm"),
                ["ngay"] = local.ToString("dd/MM"),
            }) ?? $"JP Center nhắc lịch {(a.Kind == AppointmentKind.Test ? "test xếp lớp" : "tư vấn")} lúc {local:HH:mm dd/MM}.";
            var log = MessageLog.Create(template?.Channel ?? ContactChannel.Zalo, scenario, template?.Id, lead.FullName, lead.Phone, lead.Id, null, body,
                null, now);
            if (log.IsSuccess) db.MessageLogs.Add(log.Value);
            notices.AppointmentSoon(a.StaffId, $"Sắp có lịch {(a.Kind == AppointmentKind.Test ? "test" : "tư vấn")}: {lead.FullName}",
                $"{a.Code} lúc {local:HH:mm} — {lead.Phone}");
            a.MarkReminded(now);
        }
        await db.SaveChangesAsync(ct);
        return due.Count;
    }
}
