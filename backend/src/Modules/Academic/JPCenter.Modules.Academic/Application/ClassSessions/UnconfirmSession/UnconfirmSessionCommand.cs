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
namespace JPCenter.Modules.Academic.Application.ClassSessions.UnconfirmSession;

/// <summary>
/// DT-15 — bỏ xác nhận → quay lại "chờ xác nhận", Finance bỏ khỏi bảng lương chưa chốt.
/// Tháng lương GV của chi nhánh lớp đã chốt → <c>finance.payroll.locked</c> (TC-13.E2, hỏi <c>Finance.Contracts.IPayrollLocks</c>).
/// </summary>
public sealed record UnconfirmSessionCommand(Guid Id) : ICommand<SessionResult>;

public sealed class UnconfirmSessionHandler(IAcademicDbContext db, SessionReader reader, IIntegrationEventPublisher events, IPayrollLocks payroll,
    IAuditTrail audit)
    : ICommandHandler<UnconfirmSessionCommand, SessionResult>
{
    public async Task<Result<SessionResult>> Handle(UnconfirmSessionCommand cmd, CancellationToken ct)
    {
        if (await reader.ForUpdateAsync(cmd.Id, PermissionAction.Approve, ct) is not var (session, cls)) return SessionErrors.NotFound;
        if (await payroll.IsTeacherPayrollLockedAsync(cls.BranchId.Value, session.Date, ct)) return SessionErrors.PayrollLocked;
        if (session.Unconfirm() is { IsFailure: true } r) return r.Error;
        events.Publish(new Ev.SessionUnconfirmed(session.Id.Value, cls.Id.Value, session.Date));
        await db.SaveChangesAsync(ct);
        audit.Record(AuditCategory.Data, AuditSeverity.Warning, "Bỏ xác nhận buổi học", $"{cls.Code} · buổi {session.No}");
        return new SessionResult(await reader.ReloadAsync(session.Id, ct), []);
    }
}
