using JPCenter.Modules.Hr.Contracts.Dtos;

namespace JPCenter.Modules.Hr.Contracts;

/// <summary>Đơn từ đã / đang duyệt cho module khác — Work đánh dấu ngày nghỉ trên lưới xếp ca (XC-01.E5) và chặn bỏ ca đang có đơn đổi (XC-01.E6).</summary>
public interface IHrLeaveCalendar
{
    /// <summary>Đơn nghỉ phép / công tác ĐÃ DUYỆT của các nhân viên có giao với khoảng ngày</summary>
    Task<IReadOnlyList<ApprovedLeave>> ApprovedLeavesAsync(IReadOnlyCollection<Guid> employeeIds, DateOnly from, DateOnly to, CancellationToken ct);

    /// <summary>Các ca (Work.ShiftAssignment) trong <paramref name="shiftIds"/> đang nằm trong đơn đổi ca CHỜ DUYỆT</summary>
    Task<IReadOnlySet<Guid>> PendingSwapShiftIdsAsync(IReadOnlyCollection<Guid> shiftIds, CancellationToken ct);
}
