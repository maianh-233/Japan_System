using JPCenter.SharedKernel.Domain;
using JPCenter.SharedKernel.Primitives;
using JPCenter.Modules.Crm.Domain.Leads;

namespace JPCenter.Modules.Crm.Domain.Appointments;

/// <summary>
/// Lịch hẹn lead đến tư vấn / làm test tại trung tâm (CRM-14, CRM-15). Bảng: crm.appointments.
/// Kiểm tra trùng phòng (cả buổi học), người tiếp (cả giờ dạy), lead bằng <see cref="AppointmentConflicts"/> trước khi lưu.
/// </summary>
public sealed class Appointment : AggregateRoot<AppointmentId>, IBranchScoped, IAuditable
{
    /// <summary>Nhắc trước giờ hẹn (phút)</summary>
    public const int RemindBeforeMinutes = 60;

    private Appointment() { }

    public string Code { get; private set; } = null!;
    public LeadId LeadId { get; private set; }
    public AppointmentKind Kind { get; private set; }
    public BranchId BranchId { get; private set; }
    /// <summary>Academic.Room — null = tiếp ở quầy</summary>
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

    /// <summary>Mã lịch hẹn theo số thứ tự: 1 → LH0001</summary>
    public static string CodeFor(int number) => $"LH{number:D4}";

    /// <summary>Thời lượng gợi ý (phút): tư vấn 30, làm test 60</summary>
    public static int DefaultDuration(AppointmentKind kind) => kind == AppointmentKind.Test ? 60 : 30;

    /// <summary>CRM-14 — đặt lịch (Application đã kiểm trùng). Giờ kết thúc sau giờ bắt đầu, không hẹn trong quá khứ (E1).</summary>
    public static Result<Appointment> Create(string code, LeadId leadId, AppointmentKind kind, BranchId branchId, Guid? roomId,
        Guid staffId, DateTimeOffset start, DateTimeOffset end, string? note, DateTimeOffset now)
    {
        if (end <= start || start < now) return AppointmentErrors.InvalidTime;
        return new Appointment
        {
            Id = AppointmentId.New(), Code = code, LeadId = leadId, Kind = kind, BranchId = branchId, RoomId = roomId,
            StaffId = staffId, Start = start, End = end, Note = string.IsNullOrWhiteSpace(note) ? null : note.Trim(),
            Status = AppointmentStatus.Scheduled,
        };
    }

    /// <summary>"Đổi lịch" — sửa giờ / phòng / người tiếp (kiểm trùng lại), nhắc lại từ đầu</summary>
    public Result Reschedule(DateTimeOffset start, DateTimeOffset end, Guid? roomId, Guid staffId, string? note, DateTimeOffset now)
    {
        if (Status != AppointmentStatus.Scheduled) return AppointmentErrors.NotScheduled;
        if (end <= start || start < now) return AppointmentErrors.InvalidTime;
        (Start, End, RoomId, StaffId, RemindedAt) = (start, end, roomId, staffId, null);
        Note = string.IsNullOrWhiteSpace(note) ? null : note.Trim();
        return Result.Success();
    }

    /// <summary>CRM-15 "Đã đến" — bắt buộc kết quả buổi hẹn</summary>
    public Result MarkDone(string outcome)
    {
        if (Status != AppointmentStatus.Scheduled) return AppointmentErrors.NotScheduled;
        if (string.IsNullOrWhiteSpace(outcome)) return AppointmentErrors.OutcomeRequired;
        (Status, Outcome) = (AppointmentStatus.Done, outcome.Trim());
        return Result.Success();
    }

    /// <summary>CRM-15 "Không đến" — chỉ khi đã qua giờ bắt đầu (E2)</summary>
    public Result MarkNoShow(DateTimeOffset now)
    {
        if (Status != AppointmentStatus.Scheduled) return AppointmentErrors.NotScheduled;
        if (now < Start) return AppointmentErrors.NotStarted;
        Status = AppointmentStatus.NoShow;
        return Result.Success();
    }

    /// <summary>CRM-15 "Hủy lịch" — bắt buộc lý do</summary>
    public Result Cancel(string reason)
    {
        if (Status != AppointmentStatus.Scheduled) return AppointmentErrors.NotScheduled;
        if (string.IsNullOrWhiteSpace(reason)) return AppointmentErrors.CancelReasonRequired;
        (Status, CancelReason) = (AppointmentStatus.Cancelled, reason.Trim());
        return Result.Success();
    }

    /// <summary>Job nhắc mỗi 5 phút: lịch còn hẹn, chưa nhắc, sắp bắt đầu trong 60 phút</summary>
    public bool NeedsReminder(DateTimeOffset now) =>
        Status == AppointmentStatus.Scheduled && RemindedAt is null && Start > now && Start - now <= TimeSpan.FromMinutes(RemindBeforeMinutes);

    public void MarkReminded(DateTimeOffset at) => RemindedAt ??= at;

    /// <summary>Giao giờ với khoảng [start, end) khác</summary>
    public bool Overlaps(DateTimeOffset start, DateTimeOffset end) => Start < end && start < End;
}
