using JPCenter.Modules.Academic.Application.Abstractions;
using JPCenter.Modules.Academic.Domain.Classes;
using JPCenter.Modules.System.Contracts;

namespace JPCenter.Modules.Academic.Application.Classes.Common;

/// <summary>
/// TH-08 — lớp online chưa có link → nhờ System tạo 1 phòng định kỳ cho cả lớp (Zoom → Google Meet) rồi lưu link vào lớp.
/// Gọi SAU khi lớp đã lưu: tạo phòng lỗi thì lớp vẫn giữ nguyên (E2), trả cảnh báo để giáo vụ bấm "Tạo lại link".
/// </summary>
public sealed class ClassMeetingRooms(IAcademicDbContext db, IMeetingRooms rooms)
{
    public const string NoRoomWarning = "Chưa tạo được phòng học online — lớp hiện \"Chưa có link phòng\"";

    /// <summary>Đã có link / không học online → null. Tạo được → null; không tạo được → cảnh báo.</summary>
    public async Task<string?> EnsureAsync(Class cls, CancellationToken ct)
    {
        if (!cls.NeedsMeetingRoom) return null;
        var room = await rooms.CreateAsync(cls.Id.Value, $"{cls.Code} · {cls.Name}", ct);
        if (room.Url is not { } url || cls.SetMeetingRoom(url).IsFailure) return $"{NoRoomWarning}: {room.Detail}";
        await db.SaveChangesAsync(ct);
        return null;
    }
}
