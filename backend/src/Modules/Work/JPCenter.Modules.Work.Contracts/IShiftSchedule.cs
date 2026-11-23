using JPCenter.Modules.Work.Contracts.Dtos;

namespace JPCenter.Modules.Work.Contracts;

/// <summary>
/// Lịch ca đã xếp (work.shift_assignments) cho module khác — Hr tính "giờ phải làm" khi chấm công (CC-02) và kiểm đơn đổi ca (DN-01.E6, DN-02.E6).
/// </summary>
public interface IShiftSchedule
{
    /// <summary>Ca của các nhân viên trong khoảng ngày (gồm cả 2 đầu)</summary>
    Task<IReadOnlyList<ScheduledShift>> OfEmployeesAsync(IReadOnlyCollection<Guid> employeeIds, DateOnly from, DateOnly to, CancellationToken ct);

    /// <summary>Ca theo Id; ca đã bị gỡ → không có trong kết quả</summary>
    Task<IReadOnlyDictionary<Guid, ScheduledShift>> FindManyAsync(IReadOnlyCollection<Guid> shiftIds, CancellationToken ct);
}
