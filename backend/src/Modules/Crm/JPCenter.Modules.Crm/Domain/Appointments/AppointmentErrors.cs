using JPCenter.SharedKernel.Primitives;

namespace JPCenter.Modules.Crm.Domain.Appointments;

public static class AppointmentErrors
{
    public static readonly Error NotFound = Error.NotFound("crm.appointment.not_found", "Không tìm thấy lịch hẹn");
    public static readonly Error InvalidTime =
        Error.Validation("crm.appointment.invalid_time", "Giờ kết thúc phải sau giờ bắt đầu và không hẹn trong quá khứ");
    public static readonly Error NotScheduled = Error.Conflict("crm.appointment.not_scheduled", "Lịch hẹn đã kết thúc hoặc đã hủy");
    public static readonly Error NotStarted = Error.Conflict("crm.appointment.not_started", "Chưa tới giờ hẹn — chưa đánh dấu \"Không đến\" được");
    public static readonly Error OutcomeRequired = Error.Validation("crm.appointment.outcome_required", "Chưa nhập kết quả buổi hẹn");
    public static readonly Error CancelReasonRequired = Error.Validation("crm.appointment.cancel_reason_required", "Cần ghi lý do hủy lịch");

    /// <summary>CRM-14.E6 — lỗi chung khi trùng; chi tiết trong danh sách <see cref="AppointmentConflict"/></summary>
    public static readonly Error Conflict = Error.Conflict("crm.appointment.conflict", "Lịch bị trùng — đổi giờ, phòng hoặc người tiếp");
}
