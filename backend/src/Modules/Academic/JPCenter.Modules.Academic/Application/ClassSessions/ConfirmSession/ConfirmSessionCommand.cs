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
using JPCenter.Modules.Finance.Contracts;
using JPCenter.Modules.Academic.Domain.Rooms;
using JPCenter.SharedKernel.Domain;
using JPCenter.SharedKernel.Primitives;
using Microsoft.EntityFrameworkCore;
using Ev = JPCenter.Modules.Academic.Contracts.IntegrationEvents;
namespace JPCenter.Modules.Academic.Application.ClassSessions.ConfirmSession;

/// <summary>DT-15 — xác nhận buổi đã dạy → căn cứ lương GV</summary>
public sealed record ConfirmSessionCommand(Guid Id, string? Note) : ICommand<SessionResult>;

public sealed class ConfirmSessionValidator : AbstractValidator<ConfirmSessionCommand>
{
    public ConfirmSessionValidator() => RuleFor(x => x.Note).MaximumLength(1000);
}

public sealed class ConfirmSessionHandler(IAcademicDbContext db, SessionReader reader, SessionConfirmer confirmer, IPayrollLocks payroll,
    IAuditTrail audit) : ICommandHandler<ConfirmSessionCommand, SessionResult>
{
    public async Task<Result<SessionResult>> Handle(ConfirmSessionCommand cmd, CancellationToken ct)
    {
        if (await reader.ForUpdateAsync(cmd.Id, PermissionAction.Approve, ct) is not var (session, cls)) return SessionErrors.NotFound;
        if (await payroll.IsTeacherPayrollLockedAsync(cls.BranchId.Value, session.Date, ct)) return SessionErrors.PayrollLocked;
        var done = confirmer.Confirm(session, cls, cmd.Note);
        if (done.IsFailure) return done.Error;
        await db.SaveChangesAsync(ct);
        audit.Record(AuditCategory.Data, AuditSeverity.Info, "Xác nhận buổi đã dạy", $"{cls.Code} · buổi {session.No}");
        return new SessionResult(await reader.ReloadAsync(session.Id, ct), done.Value);
    }
}
