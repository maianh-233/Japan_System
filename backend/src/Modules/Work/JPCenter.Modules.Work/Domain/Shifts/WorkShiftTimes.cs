using JPCenter.SharedKernel.Primitives;

namespace JPCenter.Modules.Work.Domain.Shifts;

/// <summary>Giờ ca làm việc của nhân viên (khác ca học) — khớp `WORK_SHIFTS` frontend</summary>
public static class WorkShiftTimes
{
    public static (TimeOnly From, TimeOnly To, string Short) Of(ShiftKey shift) => shift switch
    {
        ShiftKey.Morning => (new TimeOnly(8, 0), new TimeOnly(12, 0), "S"),
        ShiftKey.Afternoon => (new TimeOnly(13, 0), new TimeOnly(17, 30), "C"),
        _ => (new TimeOnly(17, 30), new TimeOnly(21, 30), "T"),
    };

    /// <summary>Ca kế tiếp trong ngày (null = ca cuối)</summary>
    public static ShiftKey? Next(ShiftKey shift) => shift == ShiftKey.Evening ? null : shift + 1;
}
