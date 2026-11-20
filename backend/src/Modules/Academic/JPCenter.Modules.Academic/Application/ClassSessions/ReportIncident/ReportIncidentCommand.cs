using FluentValidation;
using JPCenter.Application.Common.Auditing;
using JPCenter.Application.Common.IntegrationEvents;
using JPCenter.Application.Common.Messaging;
using JPCenter.Application.Common.Security;
using JPCenter.Modules.Academic.Application.Abstractions;
using JPCenter.Modules.Academic.Application.Classes.Common;
using JPCenter.Modules.Academic.Application.ClassSessions.Common;
using JPCenter.Modules.Academic.Application.Common;
using JPCenter.Modules.Academic.Domain.Classes;
using JPCenter.Modules.Academic.Domain.ClassSessions;
using JPCenter.Modules.Academic.Domain.Rooms;
using JPCenter.SharedKernel.Domain;
using JPCenter.SharedKernel.Primitives;
using Microsoft.EntityFrameworkCore;
using Ev = JPCenter.Modules.Academic.Contracts.IntegrationEvents;
namespace JPCenter.Modules.Academic.Application.ClassSessions.ReportIncident;

/// <summary>
/// DT-13 — báo sự cố của GV giữa buổi (chỉ khi buổi đã bắt đầu); ghi kèm nhật ký hỗ trợ và chấm công GV theo sự cố
/// (<see cref="IncidentAttendance.OnReport"/>). <paramref name="TakeoverAt"/> = giờ người dạy thay vào lớp (mặc định sự cố + 10 phút).
/// </summary>
public sealed record ReportIncidentCommand(Guid Id, TimeOnly At, IncidentReason Reason, string? Detail, IncidentAction Action, Guid? TakeoverId,
    bool Notified, TimeOnly? TakeoverAt = null) : ICommand<SessionResult>;

public sealed class ReportIncidentValidator : AbstractValidator<ReportIncidentCommand>
{
    public ReportIncidentValidator()
    {
        RuleFor(x => x.Detail).MaximumLength(1000);
        RuleFor(x => x.TakeoverId).NotNull().WithMessage("Chọn người dạy tiếp")
            .When(x => x.Action is IncidentAction.Substitute or IncidentAction.Assistant);
    }
}

public sealed class ReportIncidentHandler(IAcademicDbContext db, SessionReader reader, AcademicAccess access, IClock clock, IAuditTrail audit)
    : ICommandHandler<ReportIncidentCommand, SessionResult>
{
    public async Task<Result<SessionResult>> Handle(ReportIncidentCommand cmd, CancellationToken ct)
    {
        if (await reader.ForUpdateAsync(cmd.Id, PermissionAction.Update, ct) is not var (session, cls)) return SessionErrors.NotFound;
        var incident = new SessionIncident(cmd.At, cmd.Reason, string.IsNullOrWhiteSpace(cmd.Detail) ? null : cmd.Detail.Trim(), cmd.Action,
            cmd.Action is IncidentAction.Substitute or IncidentAction.Assistant ? cmd.TakeoverId : null, cmd.Notified, access.ActorId, clock.UtcNow);
        if (session.ReportIncident(incident, clock.LocalNow) is { IsFailure: true } r) return r.Error;
        IncidentAttendance.OnReport(session, cls, incident, cmd.TakeoverAt, clock.UtcNow);
        session.AddSupport(SupportKind.Incident, $"{IncidentAttendance.ReasonText(cmd.Reason)} lúc {TimeText.Of(cmd.At)}"
            + (incident.Detail is { } d ? $" ({d})" : "") + $" → {ActionText(cmd.Action)}", access.ActorId, clock.UtcNow);
        await db.SaveChangesAsync(ct);
        audit.Record(AuditCategory.Data, AuditSeverity.Warning, "Báo sự cố buổi học", $"{cls.Code} · buổi {session.No}", incident.Detail);
        var warnings = cmd.Action == IncidentAction.EndEarly ? new List<string> { "Kết thúc sớm — nên xếp buổi bù phần còn lại" } : [];
        return new SessionResult(await reader.ReloadAsync(session.Id, ct), warnings);
    }

    private static string ActionText(IncidentAction a) => a switch
    {
        IncidentAction.Pause => "Tạm dừng lớp",
        IncidentAction.Substitute => "GV khác dạy tiếp",
        IncidentAction.Assistant => "Trợ giảng dạy tiếp",
        _ => "Kết thúc sớm, xếp bù",
    };
}
