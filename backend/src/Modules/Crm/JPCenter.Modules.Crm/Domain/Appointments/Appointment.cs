using JPCenter.SharedKernel.Domain;
using JPCenter.SharedKernel.Primitives;
using JPCenter.Modules.Crm.Domain.Leads;

namespace JPCenter.Modules.Crm.Domain.Appointments;

/// <summary>
/// Lịch hẹn lead đến tư vấn / làm test tại trung tâm. Bảng: crm.appointments.
/// Kiểm tra trùng phòng (cả buổi học), người tiếp (cả giờ dạy), lead ở tầng Application.
/// </summary>
public sealed class Appointment : AggregateRoot<AppointmentId>, IBranchScoped, IAuditable
{
    private Appointment() { }

    public string Code { get; private set; } = null!;
    public LeadId LeadId { get; private set; }
    public AppointmentKind Kind { get; private set; }
    public BranchId BranchId { get; private set; }
    /// <summary>Academic.Room</summary>
    public Guid? RoomId { get; private set; }
    /// <summary>Người tiếp / coi test (Hr.Employee)</summary>
    public Guid StaffId { get; private set; }
    public DateTimeOffset Start { get; private set; }
    public DateTimeOffset End { get; private set; }
    public AppointmentStatus Status { get; private set; }
    public string? Note { get; private set; }
    /// <summary>Kết quả sau buổi hẹn</summary>
    public string? Outcome { get; private set; }
    /// <summary>Đã nhắc trước 1 giờ</summary>
    public DateTimeOffset? RemindedAt { get; private set; }
    public string? CancelReason { get; private set; }

    public static Result<Appointment> Create(string code, LeadId leadId, AppointmentKind kind, BranchId branchId, Guid? roomId,
        Guid staffId, DateTimeOffset start, DateTimeOffset end, string? note)
    {
        if (end <= start) return Error.Validation("crm.appointment.invalid_time", "Giờ kết thúc phải sau giờ bắt đầu");
        return new Appointment
        {
            Id = AppointmentId.New(), Code = code, LeadId = leadId, Kind = kind, BranchId = branchId, RoomId = roomId,
            StaffId = staffId, Start = start, End = end, Note = note, Status = AppointmentStatus.Scheduled,
        };
    }

    public Result Reschedule(DateTimeOffset start, DateTimeOffset end, Guid? roomId, Guid staffId)
    {
        if (Status != AppointmentStatus.Scheduled) return AppointmentErrors.NotScheduled;
        if (end <= start) return Error.Validation("crm.appointment.invalid_time", "Giờ kết thúc phải sau giờ bắt đầu");
        (Start, End, RoomId, StaffId, RemindedAt) = (start, end, roomId, staffId, null);
        return Result.Success();
    }

    public Result MarkDone(string? outcome) => Close(AppointmentStatus.Done, outcome);
    public Result MarkNoShow() => Close(AppointmentStatus.NoShow, null);

    public Result Cancel(string reason)
    {
        if (Status != AppointmentStatus.Scheduled) return AppointmentErrors.NotScheduled;
        (Status, CancelReason) = (AppointmentStatus.Cancelled, reason);
        return Result.Success();
    }

    public void MarkReminded(DateTimeOffset at) => RemindedAt = at;

    private Result Close(AppointmentStatus status, string? outcome)
    {
        if (Status != AppointmentStatus.Scheduled) return AppointmentErrors.NotScheduled;
        (Status, Outcome) = (status, outcome);
        return Result.Success();
    }
}
