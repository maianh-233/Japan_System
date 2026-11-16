using JPCenter.SharedKernel.Primitives;

namespace JPCenter.Modules.Crm.Domain.Appointments;

/// <summary>Id lịch hẹn</summary>
public readonly record struct AppointmentId(Guid Value)
{
    public static AppointmentId New() => new(IdGenerator.NewId());
}
