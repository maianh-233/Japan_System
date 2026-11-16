using JPCenter.SharedKernel.Primitives;

namespace JPCenter.Modules.Crm.Domain.Appointments;

public static class AppointmentErrors
{
    public static readonly Error NotScheduled = Error.Conflict("crm.appointment.not_scheduled", "Lịch hẹn đã kết thúc hoặc đã hủy");
}
