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

/// <summary>
/// HV-11 — lớp kết thúc (DT-04) → ghi danh Đang học thành Hoàn thành (ngày kết thúc thực tế); Bảo lưu giữ nguyên (HV-07.E4).
/// Chạy lại an toàn: chỉ xử lý ghi danh còn "đang học".
/// </summary>
public sealed class CompleteEnrollmentsOnClassFinished(IStudentsDbContext db, IIntegrationEventPublisher events)
    : IIntegrationEventHandler<ClassFinished>
{
    public async Task Handle(ClassFinished e, CancellationToken ct)
    {
        var list = await db.Enrollments.Where(x => x.ClassId == e.ClassId && x.Status == EnrollmentStatus.Studying).ToListAsync(ct);
        foreach (var x in list)
            if (x.Complete(e.EndDate).IsSuccess) events.Publish(EnrollmentRules.Ended(x, null));
        if (list.Count > 0) await db.SaveChangesAsync(ct);
    }
}
