using JPCenter.SharedKernel.Events;

namespace JPCenter.Modules.System.Contracts.IntegrationEvents;

/// <summary>Đã cấp tài khoản đăng nhập cho nhân viên (PQ-08). Hr đánh dấu hồ sơ "đã có tài khoản" (NS-01.E4).</summary>
/// <param name="EmployeeId">Hr.Employee</param>
public sealed record StaffAccountCreated(Guid EmployeeId, Guid AccountId, string Email) : IIntegrationEvent;
