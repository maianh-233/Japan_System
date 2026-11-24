using JPCenter.SharedKernel.Events;

namespace JPCenter.Modules.Finance.Contracts.IntegrationEvents;

/// <summary>
/// Đã chốt bảng lương 1 chi nhánh (lần đầu hoặc chốt lại) — Finance gửi phiếu lương từng người (email + chuông) ở nền.
/// </summary>
/// <param name="Kind">"teacher" · "staff"</param>
/// <param name="Month">YYYY-MM</param>
/// <param name="EmployeeIds">Người có phiếu lương trong bản chốt</param>
public sealed record PayrollLocked(Guid LockId, string Kind, string Month, Guid BranchId, decimal Total, IReadOnlyList<Guid> EmployeeIds)
    : IIntegrationEvent;
