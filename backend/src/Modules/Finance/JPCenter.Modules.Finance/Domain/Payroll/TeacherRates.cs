using JPCenter.SharedKernel.Primitives;

namespace JPCenter.Modules.Finance.Domain.Payroll;

/// <summary>Đơn giá 1 giờ dạy của GV (hồ sơ Hr): lớp tại trung tâm / lớp online</summary>
public sealed record TeacherRates(Money Offline, Money Online)
{
    public Money For(bool online) => online ? Online : Offline;
}
