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
using JPCenter.Modules.Academic.Domain.Requests;
using JPCenter.Modules.Academic.Domain.Rooms;
using JPCenter.SharedKernel.Domain;
using JPCenter.SharedKernel.Primitives;
using Microsoft.EntityFrameworkCore;
using Ev = JPCenter.Modules.Academic.Contracts.IntegrationEvents;
namespace JPCenter.Modules.Academic.Application.ClassSessions.CancelSession;

/// <summary>DT-11 — hủy 1 buổi trước giờ bắt đầu (lý do bắt buộc). Buổi thi → cảnh báo xếp lại kỳ thi (E3).</summary>
public sealed record CancelSessionCommand(Guid Id, string Reason) : ICommand<SessionResult>;

public sealed class CancelSessionValidator : AbstractValidator<CancelSessionCommand>
{
    public CancelSessionValidator() => RuleFor(x => x.Reason).NotEmpty().WithMessage("Nhập lý do hủy").MaximumLength(500);
}

public sealed class CancelSessionHandler(IAcademicDbContext db, SessionReader reader, AcademicAccess access, IClock clock,
    IIntegrationEventPublisher events, IAuditTrail audit) : ICommandHandler<CancelSessionCommand, SessionResult>
{
    public const string ClosedBySessionCancel = "Buổi học đã bị hủy — đơn tự đóng";

    public async Task<Result<SessionResult>> Handle(CancelSessionCommand cmd, CancellationToken ct)
    {
        if (await reader.ForUpdateAsync(cmd.Id, PermissionAction.Update, ct) is not var (session, cls)) return SessionErrors.NotFound;
        if (session.Cancel(cmd.Reason, access.ActorId, clock.UtcNow, clock.LocalNow) is { IsFailure: true } r) return r.Error;

        events.Publish(new Ev.SessionCancelled(session.Id.Value, cls.Id.Value, session.Date, session.Note!));

        // DT-11.E4: đơn xin nghỉ đang chờ của buổi → tự đóng; đơn chờ chọn buổi này làm buổi bù → bỏ đăng ký bù
        var pending = await db.LeaveRequests
            .Where(l => l.Status == RequestStatus.Pending && (l.SessionId == session.Id || l.MakeupSessionId == session.Id)).ToListAsync(ct);
        foreach (var l in pending)
            if (l.SessionId == session.Id) l.Reject(access.ActorId, clock.UtcNow, ClosedBySessionCancel);
            else l.ChangeMakeup(null);
        await db.SaveChangesAsync(ct);
        audit.Record(AuditCategory.Data, AuditSeverity.Warning, "Hủy buổi học", $"{cls.Code} · buổi {session.No}", session.Note);
        var warnings = new List<string> { "Có thể xếp buổi học bù cho buổi này" };
        if (session.Kind == SessionKind.Exam) warnings.Insert(0, "Buổi thi — cần xếp lại kỳ thi");
        return new SessionResult(await reader.ReloadAsync(session.Id, ct), warnings);
    }
}
