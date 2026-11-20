namespace JPCenter.Modules.Students.Contracts.Dtos;

/// <summary>Thông tin học viên module khác được đọc (cổng HV / PH: tên con, mã HV). Không có ghi chú nội bộ / SĐT.</summary>
public sealed record StudentSummary(Guid Id, string Code, string FullName, DateOnly? Dob, Guid BranchId);
