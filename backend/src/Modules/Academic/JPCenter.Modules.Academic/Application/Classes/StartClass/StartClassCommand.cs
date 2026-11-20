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
namespace JPCenter.Modules.Academic.Application.Classes.StartClass;

/// <summary>
/// DT-03 — khai giảng với ngày thực tế. Sĩ số dưới tối thiểu chỉ cảnh báo, 0 HV mới chặn (khi đã biết sĩ số).
/// <paramref name="Reschedule"/> → sinh lại các buổi chưa dạy từ ngày khai giảng thực tế (bỏ ngày lễ).
/// </summary>
public sealed record StartClassCommand(Guid Id, DateOnly ActualStartDate, bool Reschedule) : ICommand<ClassResult>;

public sealed class StartClassHandler(IAcademicDbContext db, ClassReader reader, ClassRoster roster, SessionScheduler scheduler,
    IIntegrationEventPublisher events, IAuditTrail audit) : ICommandHandler<StartClassCommand, ClassResult>
{
    public async Task<Result<ClassResult>> Handle(StartClassCommand cmd, CancellationToken ct)
    {
        var cls = await reader.ForUpdateAsync(cmd.Id, AcademicAccess.Classes, PermissionAction.Update, ct);
        if (cls is null) return ClassErrors.NotFound;
        var enrolled = await roster.EnrolledAsync(cls.Id.Value, ct);
        if (cls.Start(cmd.ActualStartDate, enrolled) is { IsFailure: true } r) return r.Error;

        var warnings = new List<string>();
        if (enrolled is { } n && n < cls.MinStudents) warnings.Add($"Lớp mới có {n} HV, chưa đủ tối thiểu {cls.MinStudents}");
        if (cmd.Reschedule)
        {
            var plan = await scheduler.ApplyAsync(cls, new ScheduleOptions(cmd.ActualStartDate, null, null, null, null), ct);
            warnings.Add($"Đã sinh lại {plan.Count} buổi từ {cmd.ActualStartDate:dd/MM/yyyy}");
        }
        events.Publish(new Ev.ClassStarted(cls.Id.Value, cls.Code, cmd.ActualStartDate));
        await db.SaveChangesAsync(ct);
        audit.Record(AuditCategory.Data, AuditSeverity.Info, "Khai giảng lớp", cls.Code, $"{cmd.ActualStartDate:dd/MM/yyyy}");
        return new ClassResult(await reader.ReloadAsync(cls.Id, ct), warnings);
    }
}
