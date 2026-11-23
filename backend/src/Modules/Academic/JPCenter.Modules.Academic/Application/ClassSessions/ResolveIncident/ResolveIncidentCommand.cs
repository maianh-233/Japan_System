using JPCenter.Application.Common.Time;
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
namespace JPCenter.Modules.Academic.Application.ClassSessions.ResolveIncident;

/// <summary>
/// DT-13 — kết quả sau khi tạm dừng: lớp học tiếp lúc <paramref name="At"/> (GV cũ — <paramref name="TeacherId"/> trống / là GV của buổi —
/// hoặc người khác) hoặc cho nghỉ luôn. Chấm công điều chỉnh theo (<see cref="IncidentAttendance.OnResume"/>).
/// </summary>
public sealed record ResolveIncidentCommand(Guid Id, IncidentOutcome Outcome, Guid? TeacherId, TimeOnly? At = null) : ICommand<SessionResult>;

public sealed class ResolveIncidentHandler(IAcademicDbContext db, SessionReader reader, AcademicAccess access, IClock clock, IAuditTrail audit)
    : ICommandHandler<ResolveIncidentCommand, SessionResult>
{
    public async Task<Result<SessionResult>> Handle(ResolveIncidentCommand cmd, CancellationToken ct)
    {
        if (await reader.ForUpdateAsync(cmd.Id, PermissionAction.Update, ct) is not var (session, cls)) return SessionErrors.NotFound;
        if (session.Incident is not { } inc) return SessionErrors.NoIncident;
        var leads = SessionReader.Teachers(session, cls);
        var other = cmd.Outcome == IncidentOutcome.Resumed && cmd.TeacherId is { } t && !leads.Contains(t) ? t : (Guid?)null;
        var at = cmd.At is { } time ? TimeText.At(session.Date, time) : clock.UtcNow;
        var teacher = cmd.Outcome == IncidentOutcome.Resumed ? other ?? leads.FirstOrDefault() : (Guid?)null;
        if (session.ResolveIncident(cmd.Outcome, at, teacher, access.ActorId) is { IsFailure: true } r) return r.Error;
        if (cmd.Outcome == IncidentOutcome.Resumed)
        {
            var resumeAt = cmd.At ?? TimeOnly.FromDateTime(clock.LocalNow);
            IncidentAttendance.OnResume(session, cls, resumeAt, other, inc.At, clock.UtcNow);
            session.AddSupport(SupportKind.Incident, $"Lớp học tiếp lúc {TimeText.Of(resumeAt)}", access.ActorId, clock.UtcNow);
        }
        else session.AddSupport(SupportKind.Incident, "Cho lớp nghỉ phần còn lại, chờ xếp học bù", access.ActorId, clock.UtcNow);
        await db.SaveChangesAsync(ct);
        audit.Record(AuditCategory.Data, AuditSeverity.Info, "Ghi kết quả sự cố", $"{cls.Code} · buổi {session.No}",
            cmd.Outcome == IncidentOutcome.Resumed ? "Lớp học tiếp" : "Cho nghỉ");
        return new SessionResult(await reader.ReloadAsync(session.Id, ct), []);
    }
}
