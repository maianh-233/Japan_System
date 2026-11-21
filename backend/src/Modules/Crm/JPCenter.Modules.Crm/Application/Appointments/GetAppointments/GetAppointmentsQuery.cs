using JPCenter.Application.Common.Messaging;
using JPCenter.Application.Common.Security;
using JPCenter.Modules.Crm.Application.Abstractions;
using JPCenter.Modules.Crm.Application.Appointments.Common;
using JPCenter.Modules.Crm.Domain.Appointments;
using JPCenter.Modules.Crm.Domain.Leads;
using JPCenter.SharedKernel.Primitives;
using Microsoft.EntityFrameworkCore;

namespace JPCenter.Modules.Crm.Application.Appointments.GetAppointments;

/// <summary>CRM-14 bước 5 — Sắp tới · Đã qua · Lịch tuần: lịch hẹn trong khoảng thời gian (theo giờ bắt đầu), theo phạm vi chi nhánh</summary>
public sealed record GetAppointmentsQuery(DateTimeOffset? From, DateTimeOffset? To, Guid? BranchId, Guid? LeadId, Guid? StaffId,
    AppointmentStatus? Status) : IQuery<IReadOnlyList<AppointmentDto>>;

public sealed class GetAppointmentsHandler(ICrmDbContext db, AppointmentDesk desk) : IQueryHandler<GetAppointmentsQuery, IReadOnlyList<AppointmentDto>>
{
    public async Task<Result<IReadOnlyList<AppointmentDto>>> Handle(GetAppointmentsQuery query, CancellationToken ct)
    {
        var q = desk.Scope(db.Appointments.AsNoTracking(), PermissionAction.View);
        if (query.From?.ToUniversalTime() is { } from) q = q.Where(a => a.End >= from);
        if (query.To?.ToUniversalTime() is { } to) q = q.Where(a => a.Start < to);
        if (query.BranchId is { } b) q = q.Where(a => a.BranchId == new BranchId(b));
        if (query.LeadId is { } l) q = q.Where(a => a.LeadId == new LeadId(l));
        if (query.StaffId is { } s) q = q.Where(a => a.StaffId == s);
        if (query.Status is { } st) q = q.Where(a => a.Status == st);
        return Result.Success(await desk.ToDtosAsync(await q.OrderBy(a => a.Start).Take(500).ToListAsync(ct), ct));
    }
}
