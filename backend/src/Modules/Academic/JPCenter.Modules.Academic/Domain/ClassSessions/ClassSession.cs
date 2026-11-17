using JPCenter.SharedKernel.Domain;
using JPCenter.SharedKernel.Primitives;
using JPCenter.Modules.Academic.Domain.Classes;
using JPCenter.Modules.Academic.Domain.ClassSessions.Events;
using JPCenter.Modules.Academic.Domain.Curricula;
using JPCenter.Modules.Academic.Domain.Rooms;

namespace JPCenter.Modules.Academic.Domain.ClassSessions;

/// <summary>
/// Buổi học — sinh từ lịch lớp, hoặc học bù / học thêm. Chứa điểm danh HV, chấm công GV theo buổi, sự cố, nhật ký hỗ trợ,
/// báo cáo buổi của GV và xác nhận buổi đã dạy (căn cứ lương GV).
/// Trạng thái: Scheduled (chưa xác nhận — sắp tới / đang diễn ra / chờ xác nhận suy theo giờ) → Completed (đã xác nhận) · Cancelled.
/// Giờ học là giờ địa phương của chi nhánh (DT-20.E3) → các method so giờ nhận <c>now</c> theo giờ Việt Nam.
/// Bảng: academic.class_sessions (+ attendance_records, teacher_attendances, session_support_entries)
/// </summary>
public sealed class ClassSession : AggregateRoot<ClassSessionId>, IBranchScoped, IAuditable
{
    private readonly List<AttendanceRecord> _attendance = [];
    private readonly List<TeacherAttendance> _teacherAttendance = [];
    private readonly List<SupportEntry> _supportLog = [];

    private ClassSession() { }

    public ClassId ClassId { get; private set; }
    public BranchId BranchId { get; private set; }
    /// <summary>Số thứ tự buổi (1-based) — buổi học thêm không thuộc chuỗi (0)</summary>
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
    /// <summary>Đã dời lịch: ngày + giờ gốc (lần dời đầu tiên)</summary>
    public DateOnly? RescheduledFromDate { get; private set; }
    public TimeOnly? RescheduledFromStart { get; private set; }
    public TimeOnly? RescheduledFromEnd { get; private set; }
    /// <summary>Lý do thay đổi lần cuối (dời lịch / đổi GV / đổi phòng)</summary>
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

    public DateTime StartsAt => Date.ToDateTime(StartTime);
    public DateTime EndsAt => Date.ToDateTime(EndTime);
    public bool IsConfirmed => ConfirmedAt is not null;

    /// <summary>Có thay đổi riêng (dạy thay, đổi phòng, dời lịch) — sinh lại lịch sẽ mất (DT-08.E3)</summary>
    public bool HasOverrides => TeacherIds.Count > 0 || AssistantIds is not null || RoomId is not null || RescheduledFromDate is not null;

    /// <summary>
    /// Sinh lại lịch được thay buổi này không (DT-08.E2, E4): buổi thường, chưa hủy, chưa diễn ra, chưa điểm danh, chưa xác nhận.
    /// </summary>
    public bool IsReplaceable(DateTime now) =>
        Origin == SessionOrigin.Regular && Status == SessionStatus.Scheduled && !IsConfirmed && _attendance.Count == 0 && StartsAt > now;

    public static Result<ClassSession> Create(ClassId classId, BranchId branchId, int no, DateOnly date, TimeOnly start, TimeOnly end,
        SessionKind kind, LessonId? lessonId, ExamMilestoneId? examId, SessionOrigin origin, ClassSessionId? makeupForId, string qrToken)
    {
        if (end <= start) return SessionErrors.InvalidTime;
        if (origin == SessionOrigin.Makeup && makeupForId is null) return SessionErrors.MakeupForRequired;
        return new ClassSession
        {
            Id = ClassSessionId.New(), ClassId = classId, BranchId = branchId, No = origin == SessionOrigin.Extra ? 0 : no, Date = date,
            StartTime = start, EndTime = end, Kind = kind, LessonId = kind == SessionKind.Lesson ? lessonId : null,
            ExamId = kind == SessionKind.Exam ? examId : null, Origin = origin,
            MakeupForId = origin == SessionOrigin.Makeup ? makeupForId : null, AttendanceQrToken = qrToken, Status = SessionStatus.Scheduled,
        };
    }

    /// <summary>
    /// Sửa riêng 1 buổi (DT-10): dời ngày giờ, đổi GV (dạy thay), trợ giảng, phòng. Lý do bắt buộc.
    /// <paramref name="teacherIds"/> rỗng / <paramref name="assistantIds"/> null / <paramref name="roomId"/> null = theo lớp (handler chuẩn hóa).
    /// </summary>
    public Result Edit(DateOnly date, TimeOnly start, TimeOnly end, List<Guid> teacherIds, List<Guid>? assistantIds, RoomId? roomId,
        string reason)
    {
        if (Status == SessionStatus.Cancelled) return SessionErrors.Cancelled;
        if (Status != SessionStatus.Scheduled || IsConfirmed) return SessionErrors.NotScheduled;
        if (end <= start) return SessionErrors.InvalidTime;
        if (string.IsNullOrWhiteSpace(reason)) return SessionErrors.ReasonRequired;

        if (date != Date || start != StartTime || end != EndTime)
        {
            if (RescheduledFromDate is null)
                (RescheduledFromDate, RescheduledFromStart, RescheduledFromEnd) = (Date, StartTime, EndTime);
            (Date, StartTime, EndTime) = (date, start, end);
        }
        TeacherIds = [.. teacherIds.Distinct()];
        AssistantIds = assistantIds is null ? null : [.. assistantIds.Distinct()];
        RoomId = roomId;
        RescheduleReason = reason.Trim();
        return Result.Success();
    }

    /// <summary>Áp lịch / GV / phòng mới của lớp cho buổi chưa dạy không có thay đổi riêng (DT-07.E4)</summary>
    public void FollowClass(TimeOnly start, TimeOnly end)
    {
        if (Status != SessionStatus.Scheduled || IsConfirmed || HasOverrides || end <= start) return;
        (StartTime, EndTime) = (start, end);
    }

    /// <summary>Hủy 1 buổi (DT-11) — chỉ trước giờ bắt đầu. Đã bắt đầu → báo sự cố (DT-13).</summary>
    public Result Cancel(string reason, Guid by, DateTimeOffset at, DateTime now)
    {
        if (Status == SessionStatus.Cancelled) return SessionErrors.Cancelled;
        if (Status != SessionStatus.Scheduled || IsConfirmed) return SessionErrors.NotScheduled;
        if (now >= StartsAt) return SessionErrors.AlreadyStarted;
        if (string.IsNullOrWhiteSpace(reason)) return SessionErrors.ReasonRequired;
        (Status, Note, CancelledBy, CancelledAt) = (SessionStatus.Cancelled, reason.Trim(), by, at);
        Raise(new SessionCancelled(Id, ClassId, Note));
        return Result.Success();
    }

    /// <summary>Điểm danh nhiều HV (DT-09) — thêm / sửa từng dòng, HV không có trong danh sách giữ nguyên</summary>
    public Result RecordAttendance(IReadOnlyCollection<AttendanceEntry> entries, AttendanceMethod method, Guid by, DateTimeOffset at)
    {
        if (Status == SessionStatus.Cancelled) return SessionErrors.Cancelled;
        if (IsConfirmed && method != AttendanceMethod.Staff) return SessionErrors.ConfirmedLocked;
        foreach (var e in entries)
        {
            var record = _attendance.FirstOrDefault(r => r.StudentId == e.StudentId);
            if (record is null)
            {
                record = new AttendanceRecord(e.StudentId);
                _attendance.Add(record);
            }
            record.Mark(e.Status, method, e.CheckInAt, e.Note, e.LeftAt, e.LeaveReason);
        }
        (AttendanceBy, AttendanceAt) = (by, at);
        return Result.Success();
    }

    /// <summary>Vắng theo đơn xin nghỉ đã duyệt (DT-09.E7): đã có mặt / muộn thì giữ nguyên</summary>
    public void MarkExcusedByLeave(Guid studentId, Guid leaveRequestId)
    {
        var record = _attendance.FirstOrDefault(r => r.StudentId == studentId);
        if (record is null)
        {
            record = new AttendanceRecord(studentId);
            _attendance.Add(record);
        }
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

    /// <summary>
    /// Chấm công GV / trợ giảng (DT-12). GV tự chấm (<see cref="CheckSource.Self"/>) không sửa được sau khi buổi đã xác nhận.
    /// Có giờ ra mà không có giờ vào → <see cref="SessionErrors.NotCheckedIn"/>.
    /// </summary>
    public Result RecordStaffAttendance(Guid teacherId, StaffRole role, CheckSource source, TeacherAttendanceStatus status,
        TimeOnly? checkIn, TimeOnly? checkOut, Guid? substituteId, string? note, DateTimeOffset at)
    {
        if (Status == SessionStatus.Cancelled) return SessionErrors.Cancelled;
        if (IsConfirmed && source == CheckSource.Self) return SessionErrors.ConfirmedLocked;
        if (status == TeacherAttendanceStatus.Absent) (checkIn, checkOut) = (null, null);
        else if (checkOut is not null && checkIn is null) return SessionErrors.NotCheckedIn;
        if (checkIn is { } i && checkOut is { } o && o <= i) return SessionErrors.InvalidTime;

        var item = _teacherAttendance.FirstOrDefault(t => t.TeacherId == teacherId);
        if (item is null)
        {
            item = new TeacherAttendance(teacherId);
            _teacherAttendance.Add(item);
        }
        item.Record(role, source, status, checkIn, checkOut, status == TeacherAttendanceStatus.Absent ? substituteId : null,
            string.IsNullOrWhiteSpace(note) ? null : note.Trim(), at);
        return Result.Success();
    }

    public void AddSupport(SupportKind kind, string note, Guid by, DateTimeOffset at) => _supportLog.Add(new SupportEntry(kind, note.Trim(), by, at));

    /// <summary>Báo sự cố GV giữa buổi (DT-13) — chỉ khi buổi đã bắt đầu</summary>
    public Result ReportIncident(SessionIncident incident, DateTime now)
    {
        if (Status == SessionStatus.Cancelled) return SessionErrors.Cancelled;
        if (now < StartsAt) return SessionErrors.NotStarted;
        Incident = incident;
        return Result.Success();
    }

    public Result ResolveIncident(IncidentOutcome outcome, DateTimeOffset at, Guid? teacherId, Guid by)
    {
        if (Incident is null) return SessionErrors.NoIncident;
        Incident = Incident with { Outcome = outcome, OutcomeAt = at, OutcomeTeacherId = teacherId, OutcomeBy = by };
        return Result.Success();
    }

    /// <summary>
    /// GV bấm "Kết thúc buổi học" / sửa báo cáo (DT-14). Buổi chuyển "Chờ xác nhận" (suy theo giờ) — không tự xác nhận.
    /// Sau khi xác nhận chỉ sửa được trong 24 giờ.
    /// </summary>
    public Result SubmitReport(string content, string? homework, string? issues, LessonCoverage coverage, List<ChapterId> coveredChapterIds,
        Guid authorId, DateTimeOffset at)
    {
        if (Status == SessionStatus.Cancelled) return SessionErrors.Cancelled;
        if (ConfirmedAt is { } confirmed && at > confirmed.AddHours(24)) return SessionErrors.ReportLocked;
        Report = new SessionReport(content.Trim(), homework, issues, coverage, coveredChapterIds, Report?.EndedAt ?? at, authorId, at);
        return Result.Success();
    }

    /// <summary>
    /// Xác nhận buổi đã dạy (DT-15) → Finance tính lương GV. Chặn: chưa hết giờ, chưa điểm danh HV nào.
    /// Thiếu check-in / check-out / nhật ký chỉ là lưu ý (handler trả warnings).
    /// </summary>
    public Result Confirm(Guid by, DateTimeOffset at, string? note, DateTime now)
    {
        if (Status == SessionStatus.Cancelled) return SessionErrors.Cancelled;
        if (IsConfirmed) return SessionErrors.AlreadyConfirmed;
        if (now < EndsAt) return SessionErrors.NotFinished;
        if (_attendance.Count == 0) return SessionErrors.NotAttended;
        (Status, ConfirmedBy, ConfirmedAt, ConfirmNote) = (SessionStatus.Completed, by, at, string.IsNullOrWhiteSpace(note) ? null : note.Trim());
        Raise(new SessionConfirmed(Id, ClassId, Date, _teacherAttendance.Where(t => t.Status != TeacherAttendanceStatus.Absent)
            .Select(t => new ConfirmedTeacher(t.TeacherId, t.Role)).ToList()));
        return Result.Success();
    }

    /// <summary>Bỏ xác nhận → quay lại "Chờ xác nhận", chưa tính lương đến khi xác nhận lại</summary>
    public Result Unconfirm()
    {
        if (!IsConfirmed) return SessionErrors.NotConfirmed;
        (Status, ConfirmedBy, ConfirmedAt, ConfirmNote) = (SessionStatus.Scheduled, null, null, null);
        Raise(new SessionUnconfirmed(Id, ClassId, Date));
        return Result.Success();
    }
}
