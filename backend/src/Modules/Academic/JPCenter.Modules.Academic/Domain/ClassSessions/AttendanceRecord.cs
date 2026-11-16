namespace JPCenter.Modules.Academic.Domain.ClassSessions;

/// <summary>Điểm danh 1 học viên trong 1 buổi. Bảng: academic.attendance_records, khóa (class_session_id, student_id)</summary>
public sealed class AttendanceRecord
{
    private AttendanceRecord() { }

    internal AttendanceRecord(Guid studentId) => (StudentId, Status) = (studentId, AttendanceStatus.Absent);

    /// <summary>Students.Student</summary>
    public Guid StudentId { get; private set; }
    public AttendanceStatus Status { get; private set; }
    public string? Note { get; private set; }
    /// <summary>Giờ vào lớp (quét QR / được điểm danh)</summary>
    public TimeOnly? CheckInAt { get; private set; }
    public AttendanceMethod? Method { get; private set; }
    /// <summary>Về sớm giữa buổi</summary>
    public TimeOnly? LeftAt { get; private set; }
    public string? LeaveReason { get; private set; }
    /// <summary>Vắng theo đơn xin nghỉ đã duyệt (Academic.LeaveRequest)</summary>
    public Guid? LeaveRequestId { get; private set; }
    /// <summary>Buổi vắng này đã học bù ở buổi khác → tính như có mặt</summary>
    public ClassSessionId? MadeUpSessionId { get; private set; }
    public AttendanceStatus? MadeUpStatus { get; private set; }
    /// <summary>HV lớp KHÁC đến học bù: buổi gốc bị vắng</summary>
    public ClassSessionId? MakeupForSessionId { get; private set; }
    public bool IsGuest => MakeupForSessionId is not null;

    internal static AttendanceRecord Guest(Guid studentId, ClassSessionId originalSessionId, Guid leaveRequestId) =>
        new() { StudentId = studentId, Status = AttendanceStatus.Absent, MakeupForSessionId = originalSessionId, LeaveRequestId = leaveRequestId };

    internal void Mark(AttendanceStatus status, AttendanceMethod method, TimeOnly? checkInAt, string? note) =>
        (Status, Method, CheckInAt, Note) = (status, method, checkInAt, note ?? Note);

    internal void LeaveEarly(TimeOnly leftAt, string reason) => (LeftAt, LeaveReason) = (leftAt, reason);

    internal void ExcuseByLeave(Guid leaveRequestId) => (Status, LeaveRequestId) = (AttendanceStatus.Excused, leaveRequestId);

    internal void SetMadeUp(ClassSessionId sessionId, AttendanceStatus result) => (MadeUpSessionId, MadeUpStatus) = (sessionId, result);
}
