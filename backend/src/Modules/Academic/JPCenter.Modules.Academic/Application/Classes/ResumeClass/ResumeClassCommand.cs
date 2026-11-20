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
namespace JPCenter.Modules.Academic.Application.Classes.ResumeClass;

/// <summary>DT-06 — học lại; nhắc kiểm tra lịch các buổi còn lại</summary>
public sealed record ResumeClassCommand(Guid Id) : ICommand<ClassResult>;

public sealed class ResumeClassHandler(IAcademicDbContext db, ClassReader reader, IAuditTrail audit) : ICommandHandler<ResumeClassCommand, ClassResult>
{
    public async Task<Result<ClassResult>> Handle(ResumeClassCommand cmd, CancellationToken ct)
    {
        var cls = await reader.ForUpdateAsync(cmd.Id, AcademicAccess.Classes, PermissionAction.Update, ct);
        if (cls is null) return ClassErrors.NotFound;
        if (cls.Resume() is { IsFailure: true } r) return r.Error;
        await db.SaveChangesAsync(ct);
        audit.Record(AuditCategory.Data, AuditSeverity.Info, "Học lại lớp", cls.Code);
        return new ClassResult(await reader.ReloadAsync(cls.Id, ct),
            ["Kiểm tra lịch các buổi còn lại — dùng \"Tạo lịch tự động\" nếu cần sinh lại"]);
    }
}
