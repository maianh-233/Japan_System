using JPCenter.SharedKernel.Domain;
using JPCenter.SharedKernel.Primitives;
using JPCenter.Modules.Academic.Domain.Classes;
using JPCenter.Modules.Academic.Domain.ClassSessions.Events;
using JPCenter.Modules.Academic.Domain.Curricula;
using JPCenter.Modules.Academic.Domain.Rooms;

namespace JPCenter.Modules.Academic.Domain.ClassSessions;

/// <summary>
/// Buổi học — sinh từ lịch lớp, hoặc học bù / học thêm. Chứa điểm danh HV, chấm công GV theo buổi, sự cố, nhật ký hỗ trợ,
/// báo cáo buổi của GV và xác nhận buổi đã dạy (căn cứ lương GV). Bảng: academic.class_sessions
/// (+ attendance_records, teacher_attendances, session_support_entries)
/// </summary>
public sealed class ClassSession : AggregateRoot<ClassSessionId>, IBranchScoped, IAuditable
{
    private readonly List<AttendanceRecord> _attendance = [];
    private readonly List<TeacherAttendance> _teacherAttendance = [];
    private readonly List<SupportEntry> _supportLog = [];

    private ClassSession() { }

    public ClassId ClassId { get; private set; }
    public BranchId BranchId { get; private set; }
    /// <summary>Số thứ tự buổi (1-based)</summary>
    public int No { get; private set; }
    public DateOnly Date { get; private set; }
    public TimeOnly StartTime { get; private set; }
    public TimeOnly EndTime { get; private set; }
    public SessionKind Kind { get; private set; }
    /// <summary>Bài học dự kiến (Kind = Lesson)</summary>
    public LessonId? LessonId { get; private set; }
    /// <summary>Mốc thi (Kind = Exam)</summary>
    public ExamMilestoneId? ExamId { get; private set; }
    public SessionStatus Status { get; private set; }
    public SessionOrigin Origin { get; private set; }
    /// <summary>Học bù: buổi bị hủy được bù</summary>
    public ClassSessionId? MakeupForId { get; private set; }
    /// <summary>Ghi đè GV của lớp cho riêng buổi (dạy thay) — rỗng = theo lớp</summary>
    public List<Guid> TeacherIds { get; private set; } = [];
    /// <summary>Ghi đè trợ giảng của lớp cho riêng buổi — null = theo lớp</summary>
    public List<Guid>? AssistantIds { get; private set; }
    /// <summary>Đổi phòng cho riêng buổi — null = theo lớp</summary>
    public RoomId? RoomId { get; private set; }
    /// <summary>Mã QR điểm danh — cố định theo buổi</summary>
    public string AttendanceQrToken { get; private set; } = null!;
    /// <summary>Lý do hủy / ghi chú</summary>
    public string? Note { get; private set; }
    /// <summary>Đã dời lịch: ngày + giờ gốc</summary>
    public DateOnly? RescheduledFromDate { get; private set; }
    public TimeOnly? RescheduledFromStart { get; private set; }
    public TimeOnly? RescheduledFromEnd { get; private set; }
    public string? RescheduleReason { get; private set; }
    public Guid? CancelledBy { get; private set; }
    public DateTimeOffset? CancelledAt { get; private set; }
    /// <summary>Người điểm danh lần cuối</summary>
    public Guid? AttendanceBy { get; private set; }
    public DateTimeOffset? AttendanceAt { get; private set; }
    /// <summary>Nhân viên xác nhận buổi đã diễn ra → căn cứ lương GV, trừ buổi trong gói học phí</summary>
    public Guid? ConfirmedBy { get; private set; }
    public DateTimeOffset? ConfirmedAt { get; private set; }
    public string? ConfirmNote { get; private set; }
    /// <summary>Sự cố GV giữa buổi (owned, cột incident_*)</summary>
    public SessionIncident? Incident { get; private set; }
    /// <summary>Báo cáo buổi của GV (owned, cột report_*)</summary>
    public SessionReport? Report { get; private set; }
    public IReadOnlyList<AttendanceRecord> Attendance => _attendance;
    public IReadOnlyList<TeacherAttendance> TeacherAttendance => _teacherAttendance;
    public IReadOnlyList<SupportEntry> SupportLog => _supportLog;

    public static Result<ClassSession> Create(ClassId classId, BranchId branchId, int no, DateOnly date, TimeOnly start, TimeOnly end,
        SessionKind kind, LessonId? lessonId, ExamMilestoneId? examId, SessionOrigin origin, ClassSessionId? makeupForId, string qrToken)
    {
        if (end <= start) return Error.Validation("academic.session.invalid_time", "Giờ kết thúc phải sau giờ bắt đầu");
        if (origin == SessionOrigin.Makeup && makeupForId is null)
            return Error.Validation("academic.session.makeup_for_required", "Buổi học bù phải chỉ ra buổi bị hủy được bù");
        return new ClassSession
        {
            Id = ClassSessionId.New(), ClassId = classId, BranchId = branchId, No = no, Date = date, StartTime = start, EndTime = end,
            Kind = kind, LessonId = lessonId, ExamId = examId, Origin = origin, MakeupForId = makeupForId, AttendanceQrToken = qrToken,
            Status = SessionStatus.Scheduled,
        };
    }

    public Result Reschedule(DateOnly date, TimeOnly start, TimeOnly end, string? reason)
    {
        if (Status != SessionStatus.Scheduled) return SessionErrors.NotScheduled;
        if (RescheduledFromDate is null)
            (RescheduledFromDate, RescheduledFromStart, RescheduledFromEnd) = (Date, StartTime, EndTime);
        (Date, StartTime, EndTime, RescheduleReason) = (date, start, end, reason);
        return Result.Success();
    }

    public Result Cancel(string reason, Guid by, DateTimeOffset at)
    {
        if (Status != SessionStatus.Scheduled) return SessionErrors.NotScheduled;
        (Status, Note, CancelledBy, CancelledAt) = (SessionStatus.Cancelled, reason, by, at);
        Raise(new SessionCancelled(Id, ClassId, reason));
        return Result.Success();
    }

    /// <summary>Đặt GV dạy thay cho riêng buổi (nhận lời mời / giáo vụ xếp)</summary>
    public void AssignTeachers(List<Guid> teacherIds) => TeacherIds = teacherIds;

    public void AssignAssistants(List<Guid>? assistantIds) => AssistantIds = assistantIds;

    public void ChangeRoom(RoomId? roomId) => RoomId = roomId;

    /// <summary>Điểm danh / sửa điểm danh 1 học viên</summary>
    public Result MarkAttendance(Guid studentId, AttendanceStatus status, AttendanceMethod method, TimeOnly? checkInAt, string? note,
        Guid by, DateTimeOffset at)
    {
        if (Status == SessionStatus.Cancelled) return SessionErrors.Cancelled;
        var record = _attendance.FirstOrDefault(r => r.StudentId == studentId);
        if (record is null)
        {
            record = new AttendanceRecord(studentId);
            _attendance.Add(record);
        }
        record.Mark(status, method, checkInAt, note);
        (AttendanceBy, AttendanceAt) = (by, at);
        return Result.Success();
    }

    public Result MarkEarlyLeave(Guid studentId, TimeOnly leftAt, string reason)
    {
        var record = _attendance.FirstOrDefault(r => r.StudentId == studentId);
        if (record is null) return Error.NotFound("academic.session.not_attended", "Học viên chưa được điểm danh");
        record.LeaveEarly(leftAt, reason);
        return Result.Success();
    }

    /// <summary>Vắng theo đơn xin nghỉ đã duyệt</summary>
    public void MarkExcusedByLeave(Guid studentId, Guid leaveRequestId)
    {
        var record = _attendance.FirstOrDefault(r => r.StudentId == studentId);
        if (record is null) { record = new AttendanceRecord(studentId); _attendance.Add(record); }
        record.ExcuseByLeave(leaveRequestId);
    }

    /// <summary>HV lớp khác đến học bù buổi này (không thuộc sĩ số)</summary>
    public void AddMakeupGuest(Guid studentId, ClassSessionId originalSessionId, Guid leaveRequestId)
    {
        if (_attendance.Any(r => r.StudentId == studentId)) return;
        _attendance.Add(AttendanceRecord.Guest(studentId, originalSessionId, leaveRequestId));
    }

    /// <summary>Đánh dấu buổi vắng đã được học bù ở buổi khác</summary>
    public void MarkMadeUp(Guid studentId, ClassSessionId makeupSessionId, AttendanceStatus result) =>
        _attendance.FirstOrDefault(r => r.StudentId == studentId)?.SetMadeUp(makeupSessionId, result);

    /// <summary>Chấm công GV / trợ giảng cho buổi</summary>
    public Result CheckInStaff(Guid teacherId, StaffRole role, CheckSource source, TeacherAttendanceStatus status, TimeOnly? checkIn,
        TimeOnly? checkOut, Guid? substituteId, string? note, DateTimeOffset at)
    {
        if (Status == SessionStatus.Cancelled) return SessionErrors.Cancelled;
        var item = _teacherAttendance.FirstOrDefault(t => t.TeacherId == teacherId);
        if (item is null)
        {
            item = new TeacherAttendance(teacherId);
            _teacherAttendance.Add(item);
        }
        item.Record(role, source, status, checkIn, checkOut, substituteId, note, at);
        return Result.Success();
    }

    public void AddSupport(SupportKind kind, string note, Guid by, DateTimeOffset at) => _supportLog.Add(new SupportEntry(kind, note, by, at));

    public void ReportIncident(SessionIncident incident) => Incident = incident;

    public Result ResolveIncident(IncidentOutcome outcome, DateTimeOffset at, Guid? teacherId, Guid by)
    {
        if (Incident is null) return Error.NotFound("academic.session.no_incident", "Buổi học không có sự cố");
        Incident = Incident with { Outcome = outcome, OutcomeAt = at, OutcomeTeacherId = teacherId, OutcomeBy = by };
        return Result.Success();
    }

    /// <summary>GV bấm "Kết thúc buổi học" / sửa báo cáo (vẫn sửa được sau đó). Lần đầu → buổi Completed.</summary>
    public Result SubmitReport(string content, string? homework, string? issues, LessonCoverage coverage, List<ChapterId> coveredChapterIds,
        Guid authorId, DateTimeOffset at)
    {
        if (Status == SessionStatus.Cancelled) return SessionErrors.Cancelled;
        Report = new SessionReport(content, homework, issues, coverage, coveredChapterIds, Report?.EndedAt ?? at, authorId, at);
        if (Status == SessionStatus.Scheduled) Status = SessionStatus.Completed;
        return Result.Success();
    }

    /// <summary>Nhân viên xác nhận buổi đã dạy → Finance tính lương GV</summary>
    public Result Confirm(Guid by, DateTimeOffset at, string? note)
    {
        if (Status == SessionStatus.Cancelled) return SessionErrors.Cancelled;
        if (ConfirmedAt is not null) return Error.Conflict("academic.session.already_confirmed", "Buổi học đã được xác nhận");
        (Status, ConfirmedBy, ConfirmedAt, ConfirmNote) = (SessionStatus.Completed, by, at, note);
        Raise(new SessionConfirmed(Id, ClassId, Date, _teacherAttendance.Where(t => t.Status != TeacherAttendanceStatus.Absent)
            .Select(t => new ConfirmedTeacher(t.TeacherId, t.Role)).ToList()));
        return Result.Success();
    }
}
