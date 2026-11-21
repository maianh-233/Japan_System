using JPCenter.Application.Common.Messaging;
using JPCenter.Modules.Crm.Application.Appointments.Common;
using JPCenter.Modules.Crm.Domain.Appointments;
using JPCenter.Modules.Crm.Domain.Leads;
using JPCenter.SharedKernel.Primitives;

namespace JPCenter.Modules.Crm.Application.Appointments.CheckAppointment;

/// <summary>CRM-14 bước 2 — kiểm trùng ngay khi chọn giờ / phòng / người tiếp (không lưu)</summary>
public sealed record CheckAppointmentQuery(Guid? Id, Guid LeadId, Guid? RoomId, Guid StaffId, DateTimeOffset Start, DateTimeOffset End)
    : IQuery<IReadOnlyList<ConflictDto>>;

public sealed class CheckAppointmentHandler(AppointmentDesk desk) : IQueryHandler<CheckAppointmentQuery, IReadOnlyList<ConflictDto>>
{
    public async Task<Result<IReadOnlyList<ConflictDto>>> Handle(CheckAppointmentQuery q, CancellationToken ct)
    {
        var found = await desk.ConflictsAsync(q.Id is { } id ? new AppointmentId(id) : null, new LeadId(q.LeadId), q.RoomId, q.StaffId, q.Start, q.End, ct);
        return found.Select(c => new ConflictDto(c.Kind, c.Message)).ToList();
    }
}
