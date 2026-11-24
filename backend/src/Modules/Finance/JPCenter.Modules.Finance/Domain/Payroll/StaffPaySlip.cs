using JPCenter.SharedKernel.Primitives;

namespace JPCenter.Modules.Finance.Domain.Payroll;

/// <summary>Phiếu lương NV (TC-14)</summary>
/// <param name="PayDays">Công tính lương (≤ công chuẩn)</param>
/// <param name="BasePay">Lương cứng × công ÷ công chuẩn</param>
/// <param name="Gross">Tổng thu nhập = lương theo công + phụ cấp + hoa hồng ± điều chỉnh</param>
/// <param name="Fines">Phạt đi muộn + quên check-out</param>
/// <param name="Net">Thực nhận = tổng − phạt − BHXH − thuế (âm → 0)</param>
public sealed record StaffPaySlip(
    StaffWorkDays Days,
    int PayDays,
    Money Base,
    Money BasePay,
    Money Allowance,
    Money Commission,
    decimal Adjust,
    decimal Gross,
    Money Fines,
    Money Insurance,
    Money Tax,
    Money Net);
