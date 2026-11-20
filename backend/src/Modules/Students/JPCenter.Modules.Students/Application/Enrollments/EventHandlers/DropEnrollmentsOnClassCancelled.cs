using FluentValidation;
using JPCenter.Application.Common.Auditing;
using JPCenter.Application.Common.IntegrationEvents;
using JPCenter.Application.Common.Messaging;
using JPCenter.Application.Common.Security;
using JPCenter.Modules.Academic.Contracts;
using JPCenter.Modules.Students.Application.Abstractions;
using JPCenter.Modules.Students.Application.Common;
using JPCenter.Modules.Students.Application.Enrollments.Common;
using JPCenter.Modules.Students.Application.Students.Common;
using JPCenter.Modules.Students.Domain.Enrollments;
using JPCenter.Modules.Students.Domain.Students;
using JPCenter.SharedKernel.Domain;
using JPCenter.SharedKernel.Primitives;
using Microsoft.EntityFrameworkCore;
using Ev = JPCenter.Modules.Students.Contracts.IntegrationEvents;
using JPCenter.Modules.Academic.Contracts.IntegrationEvents;

namespace JPCenter.Modules.Students.Application.Enrollments.EventHandlers;

/// <summary>HV-10.E2 — trung tâm hủy lớp (DT-05) → mọi ghi danh hiệu lực thành Đã nghỉ, lý do "Lớp bị hủy: …". Chạy lại an toàn.</summary>
public sealed class DropEnrollmentsOnClassCancelled(IStudentsDbContext db, IClock clock, IIntegrationEventPublisher events)
    : IIntegrationEventHandler<ClassCancelled>
{
    public async Task Handle(ClassCancelled e, CancellationToken ct)
    {
        var list = await db.Enrollments.Where(x => x.ClassId == e.ClassId
            && (x.Status == EnrollmentStatus.Studying || x.Status == EnrollmentStatus.Reserved)).ToListAsync(ct);
        var reason = $"Lớp bị hủy: {e.Reason}";
        foreach (var x in list)
            if (x.Drop(clock.Today, reason).IsSuccess) events.Publish(EnrollmentRules.Ended(x, reason));
        if (list.Count > 0) await db.SaveChangesAsync(ct);
    }
}
