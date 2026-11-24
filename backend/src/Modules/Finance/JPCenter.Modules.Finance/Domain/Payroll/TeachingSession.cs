namespace JPCenter.Modules.Finance.Domain.Payroll;

/// <summary>
/// 1 buổi 1 người đã dạy (có check-in + check-out, không vắng) — đọc từ Academic. Buổi hủy / vắng / thiếu check-out KHÔNG đưa vào đây.
/// </summary>
/// <param name="SessionId">Academic.ClassSession</param>
/// <param name="ClassId">Academic.Class — buổi tính vào chi nhánh của lớp (TC-13.E5)</param>
/// <param name="Minutes">Phút thực dạy = check-out − check-in (TC-13.E6 kết thúc sớm tính theo phút thực)</param>
/// <param name="Confirmed">Giáo vụ đã xác nhận buổi (DT-15) — chưa xác nhận thì chưa tính tiền</param>
public sealed record TeachingSession(Guid SessionId, Guid ClassId, DateOnly Date, TeachingRole Role, bool Online, int Minutes, bool Late, bool Confirmed);
