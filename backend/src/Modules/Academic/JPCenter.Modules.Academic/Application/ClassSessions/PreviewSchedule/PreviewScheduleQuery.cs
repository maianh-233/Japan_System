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
namespace JPCenter.Modules.Academic.Application.ClassSessions.PreviewSchedule;

/// <summary>DT-08 bước 3 — xem trước lịch sẽ sinh (không lưu)</summary>
public sealed record PreviewScheduleQuery(Guid ClassId, ScheduleOptions Options) : IQuery<SchedulePreviewDto>;

public sealed class PreviewScheduleHandler(ClassReader classes, SessionScheduler scheduler) : IQueryHandler<PreviewScheduleQuery, SchedulePreviewDto>
{
    public async Task<Result<SchedulePreviewDto>> Handle(PreviewScheduleQuery q, CancellationToken ct)
    {
        var cls = await classes.ForUpdateAsync(q.ClassId, AcademicAccess.Sessions, PermissionAction.Create, ct);
        if (cls is null) return ClassErrors.NotFound;
        if (q.Options.EndTime is { } e && q.Options.StartTime is { } s && e <= s) return SessionErrors.InvalidTime;
        return await scheduler.PreviewAsync(cls, q.Options, ct);
    }
}
