using JPCenter.SharedKernel.Primitives;

namespace JPCenter.Modules.Finance.Domain.Payroll;

/// <summary>1 dòng buổi dạy đã xác nhận trên phiếu lương GV: đơn giá áp dụng, tiền buổi, thưởng dạy thay</summary>
public sealed record TeachingLine(TeachingSession Session, Money Rate, Money Amount, Money Bonus);
