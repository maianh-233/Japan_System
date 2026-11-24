using JPCenter.Modules.Hr.Contracts.Dtos;

namespace JPCenter.Modules.Hr.Contracts;

/// <summary>Dữ liệu nhân sự để Finance tính lương GV / NV (TC-13, TC-14). Không dùng cho mục đích khác (lương là thông tin riêng).</summary>
public interface IPayrollSource
{
    /// <summary>
    /// Người còn làm việc ở 1 thời điểm bất kỳ trong [<paramref name="from"/>, <paramref name="to"/>]: đã vào làm trước <paramref name="to"/>
    /// và chưa nghỉ việc hoặc nghỉ từ <paramref name="from"/> trở đi (TC-14.E1).
    /// </summary>
    Task<IReadOnlyList<PayProfile>> ProfilesAsync(DateOnly from, DateOnly to, CancellationToken ct);

    /// <summary>Hồ sơ lương của 1 người (kể cả đã nghỉ); không thấy → null</summary>
    Task<PayProfile?> ProfileAsync(Guid employeeId, CancellationToken ct);

    /// <summary>Công tháng của các NV trong khoảng ngày (bảng công CC-03); NV không có trong kết quả → chưa có dữ liệu</summary>
    Task<IReadOnlyDictionary<Guid, WorkDaysSummary>> WorkDaysAsync(IReadOnlyCollection<Guid> employeeIds, DateOnly from, DateOnly to, CancellationToken ct);
}
