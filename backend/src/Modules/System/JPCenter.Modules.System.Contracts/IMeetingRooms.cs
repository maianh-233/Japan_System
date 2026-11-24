using JPCenter.Modules.System.Contracts.Dtos;

namespace JPCenter.Modules.System.Contracts;

/// <summary>
/// TH-08 — tạo 1 phòng học định kỳ cho cả lớp online: Zoom (việc <c>autoRoom</c> bật) → lỗi / chưa kết nối thì Google Meet → không có → <c>Url</c> null.
/// Ghi nhật ký tích hợp; không ném lỗi nghiệp vụ.
/// </summary>
public interface IMeetingRooms
{
    /// <param name="topic">Tên phòng (mã + tên lớp)</param>
    Task<MeetingRoom> CreateAsync(Guid classId, string topic, CancellationToken ct);
}
