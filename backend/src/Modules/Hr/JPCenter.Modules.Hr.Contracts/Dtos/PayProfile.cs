namespace JPCenter.Modules.Hr.Contracts.Dtos;

/// <summary>
/// Thông tin tính lương của 1 nhân viên / GV (TC-13, TC-14) — chỉ Finance đọc, KHÔNG trả ra cổng hay trang Nhân sự khác.
/// </summary>
/// <param name="Kind">"staff" · "teacher" · "assistant"</param>
/// <param name="Status">"probation" · "active" · "leave" · "quit"</param>
/// <param name="Department">"sales"… — Sale được tính hoa hồng (TC-12)</param>
/// <param name="BaseSalary">Lương cứng theo hợp đồng (GV thường trống — tính theo giờ dạy)</param>
/// <param name="RateOffline">Đơn giá / giờ dạy lớp tại trung tâm (0 nếu không phải GV)</param>
/// <param name="RateOnline">Đơn giá / giờ dạy lớp online</param>
/// <param name="Probation">Đang thử việc — không đóng BHXH</param>
public sealed record PayProfile(
    Guid Id,
    string Code,
    string FullName,
    string Email,
    string Kind,
    string Department,
    string Status,
    Guid BranchId,
    DateOnly JoinedAt,
    DateOnly? QuitAt,
    decimal BaseSalary,
    decimal Allowance,
    decimal RateOffline,
    decimal RateOnline,
    bool Probation)
{
    public bool IsTeacher => Kind is "teacher" or "assistant";
}
