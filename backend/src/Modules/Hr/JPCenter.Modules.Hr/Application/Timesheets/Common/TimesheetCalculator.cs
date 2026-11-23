using System.Globalization;
using JPCenter.Application.Common.Time;
using JPCenter.Modules.Hr.Application.Abstractions;
using JPCenter.Modules.Hr.Domain.Employees;
using JPCenter.Modules.Hr.Domain.HrRequests;
using JPCenter.Modules.Hr.Domain.TimeRecords;
using JPCenter.Modules.System.Contracts;
using JPCenter.Modules.Work.Contracts;
using JPCenter.SharedKernel.Domain;
using Microsoft.EntityFrameworkCore;

namespace JPCenter.Modules.Hr.Application.Timesheets.Common;

/// <summary>
/// Tính bảng công (CC-02, CC-03) khi đọc: bản ghi chấm công + đơn đã duyệt (nghỉ / công tác / muộn-sớm) + giờ phải làm
/// (giờ hành chính ở Cài đặt chung, ca được xếp đọc từ Work qua <see cref="IShiftSchedule"/>). Ngày trước khi vào làm / sau ngày nghỉ việc = "off".
/// </summary>
public sealed class TimesheetCalculator(IHrDbContext db, IShiftSchedule shifts, ICenterSettings settings, IClock clock)
{
    /// <summary>"2026-11" → (ngày đầu, ngày cuối); sai định dạng → null</summary>
    public static (DateOnly From, DateOnly To)? ParseMonth(string? month)
    {
        if (!DateOnly.TryParseExact($"{month}-01", "yyyy-MM-dd", CultureInfo.InvariantCulture, DateTimeStyles.None, out var from)) return null;
        return (from, from.AddMonths(1).AddDays(-1));
    }

    public static string MonthOf(DateOnly d) => d.ToString("yyyy-MM", CultureInfo.InvariantCulture);

    public async Task<IReadOnlyDictionary<EmployeeId, IReadOnlyList<TimesheetDayDto>>> DaysAsync(IReadOnlyList<Employee> employees, DateOnly from,
        DateOnly to, CancellationToken ct)
    {
        var ids = employees.Select(e => e.Id).ToList();
        var records = await db.TimeRecords.AsNoTracking()
            .Where(r => ids.Contains(r.EmployeeId) && r.Date >= from && r.Date <= to)
            .ToListAsync(ct);
        var requests = await db.HrRequests.AsNoTracking()
            .Where(r => ids.Contains(r.EmployeeId) && r.Status == HrRequestStatus.Approved && r.From <= to && r.To >= from
                && (r.Type == HrRequestType.Leave || r.Type == HrRequestType.BusinessTrip || r.Type == HrRequestType.LateEarly))
            .ToListAsync(ct);
        var shiftIds = employees.Where(e => e.ShiftBased).Select(e => e.Id.Value).ToList();
        var scheduled = shiftIds.Count == 0 ? [] : await shifts.OfEmployeesAsync(shiftIds, from, to, ct);
        var office = (await settings.GetAsync(ct)).Office;
        var today = clock.Today;
        var now = TimeOnly.FromDateTime(clock.LocalNow);

        var recordOf = records.ToDictionary(r => (r.EmployeeId, r.Date));
        var requestsOf = requests.ToLookup(r => r.EmployeeId);
        var shiftsOf = scheduled.ToLookup(s => (s.EmployeeId, s.Date));

        var result = new Dictionary<EmployeeId, IReadOnlyList<TimesheetDayDto>>();
        foreach (var e in employees)
        {
            var mine = requestsOf[e.Id].ToList();
            var days = new List<TimesheetDayDto>();
            for (var d = from; d <= to; d = d.AddDays(1))
            {
                recordOf.TryGetValue((e.Id, d), out var rec);
                var employed = d >= e.JoinedAt && (e.QuitAt is not { } q || !e.HasQuit || d <= q);
                var expected = employed
                    ? AttendanceDay.ExpectedOf(d, e.ShiftBased, shiftsOf[(e.Id.Value, d)].Select(s => (s.From, s.To, s.Short)).ToList(),
                        office.From, office.To, office.SatTo)
                    : null;
                var onLeave = mine.Any(r => r.BlocksDays && r.Covers(d));
                var excused = mine.Any(r => r.Type == HrRequestType.LateEarly && r.Covers(d));
                var day = AttendanceDay.Evaluate(d, expected, rec?.CheckIn, rec?.CheckOut, onLeave, excused, today, now, office.LateGraceMin);
                days.Add(ToDto(day, rec));
            }
            result[e.Id] = days;
        }
        return result;
    }

    public static TimesheetTotalsDto Totals(IReadOnlyList<TimesheetDayDto> days)
    {
        bool Worked(TimesheetDayDto d) => d.State is AttendanceState.Ok or AttendanceState.Late or AttendanceState.Early or AttendanceState.LateEarly
            or AttendanceState.NoOut or AttendanceState.Working;
        bool Late(TimesheetDayDto d) => d.State is AttendanceState.Late or AttendanceState.LateEarly;
        bool Early(TimesheetDayDto d) => d.State is AttendanceState.Early or AttendanceState.LateEarly;
        return new TimesheetTotalsDto(
            days.Count(Worked), days.Count(Late), days.Where(Late).Sum(d => d.LateMin), days.Count(Early),
            days.Count(d => d.State == AttendanceState.NoOut), days.Count(d => d.State == AttendanceState.Absent),
            days.Count(d => d.State == AttendanceState.Leave), Math.Round(days.Sum(d => d.WorkedMin) / 60m, 1));
    }

    public static TimesheetEmployeeDto Employee(Employee e) => new(e.Id.Value, e.Code, e.FullName, e.BranchId.Value, e.Department, e.Position,
        e.ShiftBased, e.Status, e.LeaveBalance);

    public static TimeRecordDto ToDto(TimeRecord r) =>
        new(r.Id.Value, r.EmployeeId.Value, r.Date, TimeText.Of(r.CheckIn), TimeText.Of(r.CheckOut), Edited(r));

    private static EditedDto? Edited(TimeRecord? r) =>
        r?.EditReason is { } reason ? new EditedDto(r.EditedByName ?? "?", reason) : null;

    private static TimesheetDayDto ToDto(AttendanceDay d, TimeRecord? rec)
    {
        var problem = d.IsProblem;
        return new TimesheetDayDto(d.Date, d.State,
            d.Expected is { } w ? new WorkWindowDto(TimeText.Of(w.From), TimeText.Of(w.To), w.Label) : null,
            TimeText.Of(rec?.CheckIn), TimeText.Of(rec?.CheckOut), d.LateMin, d.EarlyMin, d.WorkedMin, problem, rec?.Id.Value, Edited(rec));
    }
}
