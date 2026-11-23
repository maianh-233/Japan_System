namespace JPCenter.Modules.Hr.Domain.TimeRecords;

/// <summary>
/// Trạng thái chấm công 1 ngày (CC-02) — hàm thuần, dùng chung bảng công cá nhân và toàn bộ nhân viên.
/// </summary>
/// <param name="LateMin">Số phút muộn (≥ 0)</param>
/// <param name="EarlyMin">Số phút về sớm (≥ 0)</param>
/// <param name="WorkedMin">Giờ công (phút) — chỉ ngày có cả giờ vào và giờ ra</param>
public sealed record AttendanceDay(DateOnly Date, AttendanceState State, WorkWindow? Expected, int LateMin, int EarlyMin, int WorkedMin)
{
    /// <summary>Ngày tính công (có mặt)</summary>
    public bool Worked => State is AttendanceState.Ok or AttendanceState.Late or AttendanceState.Early or AttendanceState.LateEarly
        or AttendanceState.NoOut or AttendanceState.Working;

    public bool IsLate => State is AttendanceState.Late or AttendanceState.LateEarly;
    public bool IsEarly => State is AttendanceState.Early or AttendanceState.LateEarly;
    /// <summary>Ngày cần giải trình</summary>
    public bool IsProblem => IsLate || IsEarly || State is AttendanceState.NoOut or AttendanceState.Absent;

    /// <param name="onLeave">Có đơn nghỉ phép / công tác đã duyệt phủ ngày</param>
    /// <param name="excused">Có đơn đi muộn / về sớm đã duyệt ngày đó</param>
    /// <param name="graceMin">Cài đặt chung `office.lateGraceMin`</param>
    public static AttendanceDay Evaluate(DateOnly date, WorkWindow? expected, TimeOnly? checkIn, TimeOnly? checkOut, bool onLeave, bool excused,
        DateOnly today, TimeOnly now, int graceMin)
    {
        var lateMin = expected is not null && checkIn is { } i ? Math.Max(0, Minutes(i) - Minutes(expected.From)) : 0;
        var earlyMin = expected is not null && checkOut is { } o ? Math.Max(0, Minutes(expected.To) - Minutes(o)) : 0;
        var worked = checkIn is { } a && checkOut is { } b ? Math.Max(0, Minutes(b) - Minutes(a)) : 0;

        AttendanceState state;
        if (onLeave) state = AttendanceState.Leave;
        else if (date > today) state = AttendanceState.Future;
        else if (expected is null) state = checkIn is null && checkOut is null ? AttendanceState.Off : AttendanceState.Ok;
        else if (checkIn is null) state = date == today && now < expected.To ? AttendanceState.Future : AttendanceState.Absent;
        else if (checkOut is null) state = date == today ? AttendanceState.Working : AttendanceState.NoOut;
        else
        {
            var late = lateMin > graceMin && !excused;
            var early = earlyMin > graceMin && !excused;
            state = late && early ? AttendanceState.LateEarly : late ? AttendanceState.Late : early ? AttendanceState.Early : AttendanceState.Ok;
        }
        return new AttendanceDay(date, state, expected, lateMin, earlyMin, worked);
    }

    /// <summary>
    /// Giờ phải làm: NV theo ca = ca sớm nhất → ca muộn nhất được xếp (không có ca → null);
    /// NV hành chính = giờ hành chính, T7 đến <paramref name="satTo"/> (null = nghỉ), CN nghỉ.
    /// </summary>
    public static WorkWindow? ExpectedOf(DateOnly date, bool shiftBased, IReadOnlyCollection<(TimeOnly From, TimeOnly To, string Short)> shifts,
        TimeOnly officeFrom, TimeOnly officeTo, TimeOnly? satTo)
    {
        if (shiftBased)
        {
            if (shifts.Count == 0) return null;
            var ordered = shifts.OrderBy(s => s.From).ToList();
            return new WorkWindow(ordered[0].From, shifts.Max(s => s.To), string.Join('+', ordered.Select(s => s.Short)));
        }
        return date.DayOfWeek switch
        {
            DayOfWeek.Sunday => null,
            DayOfWeek.Saturday => satTo is { } st ? new WorkWindow(officeFrom, st, "HC") : null,
            _ => new WorkWindow(officeFrom, officeTo, "HC"),
        };
    }

    private static int Minutes(TimeOnly t) => t.Hour * 60 + t.Minute;
}
