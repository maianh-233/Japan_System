using JPCenter.Modules.System.Contracts.Dtos;

namespace JPCenter.Modules.System.Contracts;

/// <summary>
/// Ngày nghỉ lễ lấy từ Cài đặt chung (bảng <c>system.settings</c>, khóa <c>academic.holidays</c>) — không cứng trong code (DT-08.E5).
/// Academic dùng khi sinh lịch buổi học / cảnh báo xếp buổi vào ngày lễ.
/// </summary>
public interface IHolidayCalendar
{
    /// <summary>Mọi ngày lễ đã khai báo, sắp theo ngày.</summary>
    Task<IReadOnlyList<Holiday>> ListAsync(CancellationToken ct);
}
