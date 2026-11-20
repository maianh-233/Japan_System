using JPCenter.Modules.Academic.Domain.ClassSessions;
using JPCenter.Modules.Academic.Domain.Requests;

namespace JPCenter.Modules.Academic.Application.Requests.Common;

/// <summary>Buổi được nhắc tới trong đơn — null khi buổi đã bị xóa (sinh lại lịch)</summary>
public sealed record RequestSessionDto(int No, DateOnly Date, string StartTime, string EndTime, SessionStatus Status);

/// <summary>
/// Đơn hủy buổi của GV — khớp <c>CancelRequest</c> ở <c>frontend/src/features/portal/types.ts</c>
/// (<c>decidedBy</c> = tên người duyệt, <c>makeupAt</c> = ngày GV đề xuất dạy bù).
/// </summary>
public sealed record CancelRequestDto(
    Guid Id,
    Guid SessionId,
    Guid ClassId,
    string ClassCode,
    Guid BranchId,
    RequestSessionDto? Session,
    Guid TeacherId,
    string TeacherName,
    string ReasonType,
    string Reason,
    DateTimeOffset? MakeupAt,
    RequestStatus Status,
    DateTimeOffset CreatedAt,
    Guid? DecidedById,
    string? DecidedBy,
    DateTimeOffset? DecidedAt,
    string? Note);

/// <summary>Lời mời dạy thay — khớp <c>SubInvite</c> (<c>invitedBy</c> = tên giáo vụ mời)</summary>
public sealed record InviteDto(
    Guid Id,
    Guid SessionId,
    Guid ClassId,
    string ClassCode,
    Guid BranchId,
    RequestSessionDto? Session,
    Guid? CancelRequestId,
    Guid FromTeacherId,
    string FromTeacherName,
    Guid ToTeacherId,
    string ToTeacherName,
    string Reason,
    Guid InvitedById,
    string InvitedBy,
    DateTimeOffset CreatedAt,
    DateTimeOffset RespondBy,
    InviteStatus Status,
    DateTimeOffset? RespondedAt,
    string? DeclineReason);

/// <summary>Buổi học bù đã đăng ký ở lớp khác</summary>
public sealed record MakeupSessionDto(Guid SessionId, Guid ClassId, string ClassCode, DateOnly Date, string StartTime, string EndTime);

/// <summary>Đơn xin nghỉ của HV / PH — khớp <c>LeaveRequest</c> (<c>decidedBy</c> = tên người duyệt)</summary>
public sealed record LeaveRequestDto(
    Guid Id,
    Guid StudentId,
    string StudentName,
    Guid ClassId,
    string ClassCode,
    Guid BranchId,
    Guid SessionId,
    RequestSessionDto? Session,
    string ReasonType,
    string Reason,
    RequestedBy By,
    string ByName,
    RequestStatus Status,
    DateTimeOffset CreatedAt,
    Guid? DecidedById,
    string? DecidedBy,
    DateTimeOffset? DecidedAt,
    Guid? MakeupSessionId,
    MakeupSessionDto? Makeup,
    AttendanceStatus? MakeupResult,
    string? Note);

/// <summary>Kết quả duyệt / từ chối kèm cảnh báo không chặn</summary>
public sealed record DecisionResult<T>(T Request, IReadOnlyList<string> Warnings);
