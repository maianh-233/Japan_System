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
namespace JPCenter.Modules.Academic.Application.Classes.FinishClass;

/// <summary>DT-04 — kết thúc lớp: hủy các buổi chưa dạy ("Lớp kết thúc sớm"), cảnh báo buổi đã dạy chưa xác nhận (E2)</summary>
public sealed record FinishClassCommand(Guid Id, DateOnly ActualEndDate) : ICommand<ClassResult>;

public sealed class FinishClassHandler(IAcademicDbContext db, ClassReader reader, UpcomingSessions upcoming, IIntegrationEventPublisher events,
    IAuditTrail audit, IClock clock) : ICommandHandler<FinishClassCommand, ClassResult>
{
    public async Task<Result<ClassResult>> Handle(FinishClassCommand cmd, CancellationToken ct)
    {
        var cls = await reader.ForUpdateAsync(cmd.Id, AcademicAccess.Classes, PermissionAction.Update, ct);
        if (cls is null) return ClassErrors.NotFound;
        if (cls.Finish(cmd.ActualEndDate) is { IsFailure: true } r) return r.Error;

        var warnings = new List<string>();
        var cancelled = await upcoming.CancelAsync(cls.Id, "Lớp kết thúc sớm", ct);
        if (cancelled > 0) warnings.Add($"Đã hủy {cancelled} buổi chưa dạy");
        var unconfirmed = await upcoming.UnconfirmedPastAsync(cls.Id, ct);
        if (unconfirmed > 0) warnings.Add($"{unconfirmed} buổi chưa xác nhận — chưa tính lương GV");
        // DT-17 bước 4: đề xuất lên lớp chưa chốt tự giao Sale (⏳ chọn Sale theo lead liên kết khi có Crm — giai đoạn 16)
        var promotions = await db.PromotionCandidates.Where(p => p.FromClassId == cls.Id && p.HandedOverAt == null).ToListAsync(ct);
        var handed = promotions.Count(p => p.HandOver(null, clock.UtcNow).IsSuccess);
        if (handed > 0) warnings.Add($"Đã giao Sale tư vấn lên lớp cho {handed} học viên");
        events.Publish(new Ev.ClassFinished(cls.Id.Value, cls.Code, cmd.ActualEndDate));
        await db.SaveChangesAsync(ct);
        audit.Record(AuditCategory.Data, AuditSeverity.Info, "Kết thúc lớp", cls.Code, $"{cmd.ActualEndDate:dd/MM/yyyy}");
        return new ClassResult(await reader.ReloadAsync(cls.Id, ct), warnings);
    }
}
