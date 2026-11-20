using JPCenter.Modules.Academic.Domain.ClassSessions;

namespace JPCenter.Modules.Academic.Application.ClassSessions.Common;

/// <summary>
/// Buổi học — khớp <c>ClassSession</c> ở <c>frontend/src/features/academic/classes/types.ts</c>.
/// <c>teacherIds</c> / <c>assistantIds</c> / <c>room</c> chỉ có khi buổi ghi đè lớp (dạy thay, đổi phòng); giờ dạng "HH:mm".
/// Người thao tác trả cả tên lẫn Id.
/// </summary>
public sealed record SessionDto(
    Guid Id,
    Guid ClassId,
    string ClassCode,
    string ClassName,
    Guid BranchId,
    int No,
    DateOnly Date,
    string StartTime,
    string EndTime,
    SessionKind Kind,
    Guid? LessonId,
    Guid? ExamId,
    SessionStatus Status,
    SessionOrigin Origin,
    Guid? MakeupForId,
    IReadOnlyList<Guid>? TeacherIds,
    IReadOnlyList<Guid>? AssistantIds,
    Guid? RoomId,
    string? Room,
    /// <summary>GV / phòng hiệu lực (ghi đè của buổi, không có thì theo lớp) — cho bảng buổi trong ngày</summary>
    IReadOnlyList<Guid> EffectiveTeacherIds,
    string? EffectiveRoom,
    bool Online,
    string? Note,
    AttendanceSummaryDto? Attendance,
    IReadOnlyList<AttendanceRecordDto> Records,
    PersonAtDto? AttendanceBy,
    IReadOnlyList<TeacherAttendanceDto> TeacherAttendance,
    ConfirmationDto? Confirmation,
    IncidentDto? Incident,
    IReadOnlyList<SupportEntryDto> SupportLog,
    TeacherLogDto? TeacherLog,
    RescheduledFromDto? RescheduledFrom,
    PersonAtDto? CancelledBy,
    uint RowVersion);

public sealed record AttendanceSummaryDto(int Present, int Total);

public sealed record PersonAtDto(Guid? Id, string Name, DateTimeOffset At);

public sealed record ConfirmationDto(Guid? ById, string By, DateTimeOffset At, string? Note);

public sealed record RescheduledFromDto(DateOnly Date, string StartTime, string EndTime, string? Reason);

public sealed record MadeUpDto(Guid SessionId, string? ClassCode, AttendanceStatus? Status);

public sealed record MakeupForDto(Guid SessionId, Guid? ClassId, Guid? LeaveRequestId);

public sealed record AttendanceRecordDto(
    Guid StudentId,
    AttendanceStatus Status,
    string? Note,
    string? CheckInAt,
    AttendanceMethod? Method,
    string? LeftAt,
    string? LeaveReason,
    Guid? LeaveRequestId,
    MadeUpDto? MadeUp,
    MakeupForDto? MakeupFor);

public sealed record TeacherAttendanceDto(
    Guid TeacherId,
    StaffRole Role,
    CheckSource Source,
    DateTimeOffset CheckedAt,
    TeacherAttendanceStatus Status,
    string? CheckIn,
    string? CheckOut,
    Guid? SubstituteId,
    string? Note);

public sealed record IncidentOutcomeDto(IncidentOutcome Kind, DateTimeOffset? At, Guid? TeacherId, string? By);

public sealed record IncidentDto(
    string At,
    IncidentReason Reason,
    string? Detail,
    IncidentAction Action,
    Guid? TakeoverId,
    bool Notified,
    IncidentOutcomeDto? Outcome,
    Guid ReportedById,
    string ReportedBy,
    DateTimeOffset ReportedAt);

public sealed record SupportEntryDto(Guid Id, SupportKind Kind, string Note, Guid ById, string By, DateTimeOffset At);

public sealed record TeacherLogDto(
    string Content,
    string? Homework,
    string? Issues,
    LessonCoverage Coverage,
    IReadOnlyList<Guid> CoveredChapterIds,
    DateTimeOffset EndedAt,
    Guid AuthorId,
    string Author,
    DateTimeOffset At);

/// <summary>Kết quả thao tác buổi kèm cảnh báo không chặn (trùng lịch, thiếu chấm công…)</summary>
public sealed record SessionResult(SessionDto Session, IReadOnlyList<string> Warnings);
