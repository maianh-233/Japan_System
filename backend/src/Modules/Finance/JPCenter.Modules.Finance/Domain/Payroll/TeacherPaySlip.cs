using JPCenter.SharedKernel.Primitives;

namespace JPCenter.Modules.Finance.Domain.Payroll;

/// <summary>Phiếu lương GV tạm tính / lúc chốt (TC-13)</summary>
/// <param name="Lines">Buổi đã xác nhận — được tính tiền</param>
/// <param name="Pending">Buổi chờ xác nhận — chưa tính (cảnh báo khi chốt)</param>
/// <param name="Hours">Tổng giờ dạy đã xác nhận</param>
/// <param name="Adjust">Tổng điều chỉnh (có dấu)</param>
/// <param name="Total">Thực nhận = lương cứng + tiền dạy + thưởng dạy thay − phạt muộn ± điều chỉnh (âm → 0)</param>
public sealed record TeacherPaySlip(
    IReadOnlyList<TeachingLine> Lines,
    IReadOnlyList<TeachingSession> Pending,
    decimal Hours,
    Money Base,
    Money Teaching,
    Money SubstituteBonus,
    Money LateFine,
    decimal Adjust,
    Money Total);
