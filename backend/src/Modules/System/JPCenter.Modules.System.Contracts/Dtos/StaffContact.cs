namespace JPCenter.Modules.System.Contracts.Dtos;

/// <param name="EmployeeId">Hr.Employee</param>
/// <param name="Email">Email đăng nhập (nhận thông báo)</param>
public sealed record StaffContact(Guid AccountId, Guid EmployeeId, string Email);
