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
using JPCenter.SharedKernel.Domain;
using JPCenter.SharedKernel.Primitives;
using Microsoft.EntityFrameworkCore;
using Ev = JPCenter.Modules.Academic.Contracts.IntegrationEvents;
namespace JPCenter.Modules.Academic.Application.Classes.CancelClass;

/// <summary>DT-05 — hủy lớp (không hoàn tác): lý do bắt buộc + gõ đúng mã lớp; hủy các buổi chưa dạy</summary>
public sealed record CancelClassCommand(Guid Id, string Reason, string ConfirmCode) : ICommand<ClassResult>;

public sealed class CancelClassHandler(IAcademicDbContext db, ClassReader reader, UpcomingSessions upcoming, IIntegrationEventPublisher events,
    IAuditTrail audit) : ICommandHandler<CancelClassCommand, ClassResult>
{
    public async Task<Result<ClassResult>> Handle(CancelClassCommand cmd, CancellationToken ct)
    {
        var cls = await reader.ForUpdateAsync(cmd.Id, AcademicAccess.Classes, PermissionAction.Update, ct);
        if (cls is null) return ClassErrors.NotFound;
        if (!string.Equals((cmd.ConfirmCode ?? "").Trim(), cls.Code, StringComparison.OrdinalIgnoreCase)) return ClassErrors.CancelConfirm;
        if (cls.Cancel(cmd.Reason ?? "") is { IsFailure: true } r) return r.Error;

        var cancelled = await upcoming.CancelAsync(cls.Id, "Lớp bị hủy: " + cls.CancelReason, ct);
        events.Publish(new Ev.ClassCancelled(cls.Id.Value, cls.Code, cls.CancelReason!));
        await db.SaveChangesAsync(ct);
        audit.Record(AuditCategory.Data, AuditSeverity.Warning, "Hủy lớp", cls.Code, cls.CancelReason);
        return new ClassResult(await reader.ReloadAsync(cls.Id, ct), cancelled > 0 ? [$"Đã hủy {cancelled} buổi chưa dạy"] : []);
    }
}
