using JPCenter.Modules.Academic.Domain.Rooms;

namespace JPCenter.Modules.Academic.Application.Rooms.Common;

/// <summary>Phòng học — khớp <c>Room</c> ở <c>frontend/src/features/academic/rooms/types.ts</c></summary>
public sealed record RoomDto(
    Guid Id,
    Guid BranchId,
    string Name,
    string? Floor,
    RoomType Type,
    int Capacity,
    decimal? Area,
    IReadOnlyList<Equipment> Equipment,
    RoomStatus Status,
    string? MaintenanceReason,
    DateOnly? MaintenanceUntil,
    string? Note,
    /// <summary>Mã các lớp đang mở dùng phòng (chờ khai giảng / đang học / tạm dừng)</summary>
    IReadOnlyList<string> OpenClasses,
    DateTimeOffset CreatedAt,
    uint RowVersion);

/// <summary>Ô chọn phòng ở form lớp / buổi</summary>
public sealed record RoomOptionDto(Guid Id, Guid BranchId, string Name, int Capacity, RoomStatus Status, string? MaintenanceReason);

/// <summary>Báo hư hỏng — khớp <c>RoomIssue</c> ở frontend; người báo trả cả Id lẫn tên</summary>
public sealed record RoomIssueDto(
    Guid Id,
    Guid RoomId,
    Equipment? Equipment,
    string Title,
    string? Description,
    IssueSeverity Severity,
    IssueStatus Status,
    Guid ReportedById,
    string ReportedBy,
    DateTimeOffset ReportedAt,
    string? Assignee,
    DateOnly? DueDate,
    DateTimeOffset? StartedAt,
    DateTimeOffset? ResolvedAt,
    string? Resolution,
    decimal? Cost);
