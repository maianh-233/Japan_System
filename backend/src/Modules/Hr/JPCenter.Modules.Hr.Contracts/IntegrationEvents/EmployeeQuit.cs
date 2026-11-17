using JPCenter.SharedKernel.Events;

namespace JPCenter.Modules.Hr.Contracts.IntegrationEvents;

/// <summary>
/// Nhân sự đã nghỉ việc (NS-07 — ngay khi cho nghỉ, hoặc khi tới ngày đã hẹn). System khóa tài khoản + thu hồi phiên (PQ-08.E6);
/// về sau: Work gỡ khỏi xếp ca, Academic không cho chọn khi xếp lớp / dạy thay.
/// </summary>
public sealed record EmployeeQuit(Guid EmployeeId, string FullName, DateOnly QuitAt, string Reason) : IIntegrationEvent;
