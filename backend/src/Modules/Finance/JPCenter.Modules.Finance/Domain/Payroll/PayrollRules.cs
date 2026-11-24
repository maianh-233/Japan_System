namespace JPCenter.Modules.Finance.Domain.Payroll;

/// <summary>
/// Tham số tính lương — khớp <c>frontend/src/features/finance/constants.ts</c>. Chưa đưa vào Cài đặt chung: đổi luật → sửa <see cref="Default"/>.
/// </summary>
/// <param name="SubstituteBonus">Thưởng mỗi buổi dạy thay (GV)</param>
/// <param name="TeacherLateFine">Phạt GV đi muộn mỗi buổi</param>
/// <param name="StandardDays">Công chuẩn tháng (NV)</param>
/// <param name="InsuranceRate">BHXH 8% + BHYT 1,5% + BHTN 1% trên lương cứng (NV thử việc không đóng)</param>
/// <param name="LateFine">Phạt NV đi muộn mỗi ngày</param>
/// <param name="NoCheckOutFine">Phạt NV quên check-out mỗi ngày</param>
/// <param name="PersonalDeduction">Giảm trừ gia cảnh bản thân (thuế TNCN)</param>
public sealed record PayrollRules(
    decimal SubstituteBonus,
    decimal TeacherLateFine,
    int StandardDays,
    decimal InsuranceRate,
    decimal LateFine,
    decimal NoCheckOutFine,
    decimal PersonalDeduction)
{
    public static readonly PayrollRules Default = new(50_000, 30_000, 26, 0.105m, 50_000, 30_000, 11_000_000);
}
