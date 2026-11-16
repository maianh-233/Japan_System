namespace JPCenter.Modules.Academic.Domain.ClassSessions;

/// <summary>Chấm công GV / trợ giảng cho 1 buổi. Bảng: academic.teacher_attendances, khóa (class_session_id, teacher_id)</summary>
public sealed class TeacherAttendance
{
    private TeacherAttendance() { }

    internal TeacherAttendance(Guid teacherId) => TeacherId = teacherId;

    /// <summary>Hr.Employee (GV / trợ giảng)</summary>
    public Guid TeacherId { get; private set; }
    public StaffRole Role { get; private set; }
    public CheckSource Source { get; private set; }
    public DateTimeOffset CheckedAt { get; private set; }
    public TeacherAttendanceStatus Status { get; private set; }
    public TimeOnly? CheckIn { get; private set; }
    public TimeOnly? CheckOut { get; private set; }
    /// <summary>GV vắng → người dạy thay (Hr.Employee)</summary>
    public Guid? SubstituteId { get; private set; }
    public string? Note { get; private set; }

    internal void Record(StaffRole role, CheckSource source, TeacherAttendanceStatus status, TimeOnly? checkIn, TimeOnly? checkOut,
        Guid? substituteId, string? note, DateTimeOffset at) =>
        (Role, Source, Status, CheckIn, CheckOut, SubstituteId, Note, CheckedAt) = (role, source, status, checkIn, checkOut, substituteId, note, at);
}
