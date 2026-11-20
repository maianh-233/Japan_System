namespace JPCenter.Modules.Academic.Contracts.Dtos;

/// <summary>
/// Thông tin lớp module khác được đọc (ghi danh, hóa đơn, lịch hẹn…).
/// <paramref name="Status"/>: enrolling · ongoing · paused · finished · cancelled.
/// <paramref name="TaughtSessions"/> = buổi thường + học bù đã xác nhận — gợi ý học phí theo số buổi còn lại / khấu trừ khi chuyển lớp (HV-06.E4, HV-09).
/// </summary>
public sealed record ClassSummary(
    Guid Id,
    string Code,
    string Name,
    Guid BranchId,
    Guid CourseId,
    string Status,
    int MaxStudents,
    decimal Fee,
    int SessionCount,
    int TaughtSessions)
{
    /// <summary>Nhận ghi danh mới / học lại: chờ khai giảng hoặc đang học (tạm dừng / đã đóng → <c>academic.class.closed</c>)</summary>
    public bool AcceptsStudents => Status is "enrolling" or "ongoing";

    public bool IsClosed => Status is "finished" or "cancelled";
}
