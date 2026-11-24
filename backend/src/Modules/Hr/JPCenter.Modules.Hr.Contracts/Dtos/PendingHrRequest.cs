namespace JPCenter.Modules.Hr.Contracts.Dtos;

/// <summary>Đơn từ nhân viên đang chờ người dùng hiện tại duyệt (BC-02 Giáo vụ › Đơn chờ duyệt).</summary>
/// <param name="Type">Loại đơn (snake_case, vd <c>annual_leave</c>)</param>
public sealed record PendingHrRequest(Guid Id, string Code, string Type, string EmployeeName, Guid BranchId, DateTimeOffset RequestedAt);
