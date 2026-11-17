using JPCenter.SharedKernel.Events;

namespace JPCenter.Modules.System.Contracts.IntegrationEvents;

/// <summary>
/// Chi nhánh khai trương / đóng / mở lại (CN-05, CN-06, CN-07). Module khác (CRM, Đào tạo, Học viên) dùng để chặn tạo dữ liệu mới
/// ở chi nhánh đã đóng.
/// </summary>
/// <param name="Status">Trạng thái mới: "active" · "closed"</param>
/// <param name="PreviousStatus">"preparing" · "active" · "closed"</param>
public sealed record BranchStatusChanged(Guid BranchId, string Code, string Name, string Status, string PreviousStatus, DateTimeOffset At)
    : IIntegrationEvent;
