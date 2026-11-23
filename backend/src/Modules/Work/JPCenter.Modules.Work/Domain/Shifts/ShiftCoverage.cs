using JPCenter.SharedKernel.Primitives;

namespace JPCenter.Modules.Work.Domain.Shifts;

/// <summary>XC-03 — số người tối thiểu mỗi ca theo bộ phận (trong tuần / cuối tuần). Bộ phận không có định mức → 0.</summary>
public static class ShiftCoverage
{
    /// <param name="department">Mã bộ phận Hr ("sales", "reception"...)</param>
    public static int Minimum(string department, DateOnly date, ShiftKey shift)
    {
        var weekend = date.DayOfWeek is DayOfWeek.Saturday or DayOfWeek.Sunday;
        return (department, weekend, shift) switch
        {
            ("sales", false, ShiftKey.Evening) => 2,
            ("sales", _, _) => 1,
            ("reception", false, ShiftKey.Evening) => 0,
            ("reception", false, _) => 1,
            ("reception", true, ShiftKey.Morning) => 1,
            _ => 0,
        };
    }
}
