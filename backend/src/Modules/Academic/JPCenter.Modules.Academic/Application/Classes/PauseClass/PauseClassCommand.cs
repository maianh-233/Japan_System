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
namespace JPCenter.Modules.Academic.Application.Classes.PauseClass;

/// <summary>DT-06 — tạm dừng (lý do bắt buộc); buổi chưa dạy giữ nguyên trên lịch</summary>
public sealed record PauseClassCommand(Guid Id, string Reason) : ICommand<ClassResult>;

public sealed class PauseClassHandler(IAcademicDbContext db, ClassReader reader, IAuditTrail audit) : ICommandHandler<PauseClassCommand, ClassResult>
{
    public async Task<Result<ClassResult>> Handle(PauseClassCommand cmd, CancellationToken ct)
    {
        var cls = await reader.ForUpdateAsync(cmd.Id, AcademicAccess.Classes, PermissionAction.Update, ct);
        if (cls is null) return ClassErrors.NotFound;
        if (cls.Pause(cmd.Reason ?? "") is { IsFailure: true } r) return r.Error;
        await db.SaveChangesAsync(ct);
        audit.Record(AuditCategory.Data, AuditSeverity.Warning, "Tạm dừng lớp", cls.Code, cls.PauseReason);
        return new ClassResult(await reader.ReloadAsync(cls.Id, ct), []);
    }
}
