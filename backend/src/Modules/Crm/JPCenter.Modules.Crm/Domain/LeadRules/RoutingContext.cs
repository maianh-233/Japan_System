namespace JPCenter.Modules.Crm.Domain.LeadRules;

/// <summary>
/// Dữ liệu ngoài Crm mà bộ chia lead cần — Application nạp từ Hr (còn làm việc, trong ca) và đếm lead mở.
/// </summary>
/// <param name="IsWorking">Sale còn làm việc (chưa nghỉ / chuyển phòng)</param>
/// <param name="IsOnShift">Sale đang trong ca lúc chia (theo Xếp ca, không làm theo ca → giờ hành chính)</param>
/// <param name="OpenLoad">Số lead mở (mới / đang tư vấn / hẹn test, không ẩn) Sale đang giữ</param>
/// <param name="NameOf">Tên Sale để ghi nhật ký</param>
public sealed record RoutingContext(
    Func<Guid, bool> IsWorking,
    Func<Guid, bool> IsOnShift,
    Func<Guid, int> OpenLoad,
    Func<Guid, string> NameOf);
