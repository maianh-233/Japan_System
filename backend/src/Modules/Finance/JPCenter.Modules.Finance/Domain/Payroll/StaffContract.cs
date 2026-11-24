using JPCenter.SharedKernel.Primitives;

namespace JPCenter.Modules.Finance.Domain.Payroll;

/// <summary>Thông tin hợp đồng dùng tính lương NV (hồ sơ Hr)</summary>
/// <param name="Probation">Đang thử việc — không đóng BHXH</param>
public sealed record StaffContract(Money BaseSalary, Money Allowance, bool Probation);
