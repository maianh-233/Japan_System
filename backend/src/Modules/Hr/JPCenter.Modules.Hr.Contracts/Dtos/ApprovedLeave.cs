namespace JPCenter.Modules.Hr.Contracts.Dtos;

/// <summary>Khoảng nghỉ đã duyệt. <paramref name="Type"/>: "leave" (nghỉ phép) · "business_trip" (công tác)</summary>
public sealed record ApprovedLeave(Guid EmployeeId, DateOnly From, DateOnly To, string Type);
