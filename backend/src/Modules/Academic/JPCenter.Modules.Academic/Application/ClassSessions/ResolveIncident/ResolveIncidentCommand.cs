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

/// <summary>DT-13 — kết quả sau khi tạm dừng: lớp học tiếp (ai dạy) hoặc cho nghỉ luôn</summary>
public sealed record ResolveIncidentCommand(Guid Id, IncidentOutcome Outcome, Guid? TeacherId) : ICommand<SessionResult>;

public sealed class ResolveIncidentHandler(IAcademicDbContext db, SessionReader reader, AcademicAccess access, IClock clock, IAuditTrail audit)
    : ICommandHandler<ResolveIncidentCommand, SessionResult>
{
    public async Task<Result<SessionResult>> Handle(ResolveIncidentCommand cmd, CancellationToken ct)
    {
        if (await reader.ForUpdateAsync(cmd.Id, PermissionAction.Update, ct) is not var (session, cls)) return SessionErrors.NotFound;
        var teacher = cmd.Outcome == IncidentOutcome.Resumed ? cmd.TeacherId : null;
        if (session.ResolveIncident(cmd.Outcome, clock.UtcNow, teacher, access.ActorId) is { IsFailure: true } r) return r.Error;
        await db.SaveChangesAsync(ct);
        audit.Record(AuditCategory.Data, AuditSeverity.Info, "Ghi kết quả sự cố", $"{cls.Code} · buổi {session.No}",
            cmd.Outcome == IncidentOutcome.Resumed ? "Lớp học tiếp" : "Cho nghỉ");
        return new SessionResult(await reader.ReloadAsync(session.Id, ct), []);
    }
}
