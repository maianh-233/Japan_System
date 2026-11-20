using JPCenter.Modules.Academic.Domain.Classes;

namespace JPCenter.Modules.Academic.Application.Classes.Common;

/// <summary>
/// Lớp học — khớp <c>ClassInfo</c> ở <c>frontend/src/features/academic/classes/types.ts</c> (+ Id phòng, tiến độ, sĩ số, version).
/// <c>room</c> = tên phòng như frontend; giờ dạng "HH:mm".
/// </summary>
public sealed record ClassDto(
    Guid Id,
    Guid CourseId,
    Guid? CurriculumId,
    string Code,
    string Name,
    string CourseLabel,
    Guid BranchId,
    IReadOnlyList<Guid> TeacherIds,
    IReadOnlyList<Guid> AssistantIds,
    Guid? RoomId,
    string? Room,
    bool Online,
    string? MeetingUrl,
    DateOnly PlannedStartDate,
    DateOnly PlannedEndDate,
    DateOnly? ActualStartDate,
    DateOnly? ActualEndDate,
    IReadOnlyList<int> Weekdays,
    string StartTime,
    string EndTime,
    decimal TotalHours,
    int MinStudents,
    int MaxStudents,
    /// <summary>Học phí / HV — null khi người xem không có quyền xem học phí (DT-01.E2)</summary>
    decimal? Fee,
    ClassStatus Status,
    Guid? PreviousClassId,
    string? PauseReason,
    string? CancelReason,
    string? Note,
    /// <summary>Số buổi theo khóa = tổng giờ ÷ giờ mỗi buổi</summary>
    int SessionCount,
    ClassProgressDto Progress,
    /// <summary>Sĩ số hiện tại = ghi danh đang học + bảo lưu (Students.Contracts)</summary>
    int? Enrolled,
    DateTimeOffset CreatedAt,
    uint RowVersion);

/// <summary>Tiến độ buổi: đã xác nhận / còn lịch / đã hủy (không tính buổi học thêm)</summary>
public sealed record ClassProgressDto(int Confirmed, int Scheduled, int Cancelled);

/// <summary>Kết quả thao tác có cảnh báo không chặn (khai giảng thiếu sĩ số, kết thúc còn buổi chưa xác nhận…)</summary>
public sealed record ClassResult(ClassDto Class, IReadOnlyList<string> Warnings);

/// <summary>1 lớp trùng lịch GV / phòng (DT-02.E8). <c>Kind</c>: teacher · room</summary>
public sealed record ScheduleConflictDto(Guid ClassId, string Code, string Kind, Guid? PersonId, string Detail);
