using JPCenter.Modules.Academic.Application.Abstractions;
using JPCenter.Modules.Academic.Application.Common;
using JPCenter.Modules.Academic.Domain.Classes;
using JPCenter.Modules.Academic.Domain.ClassSessions;
using JPCenter.Modules.Hr.Contracts;
using Microsoft.EntityFrameworkCore;

namespace JPCenter.Modules.Academic.Application.ClassSessions.Common;

/// <summary>1 buổi muốn xếp — để dò trùng (DT-10, DT-20)</summary>
public sealed record SessionProbe(DateOnly Date, TimeOnly StartTime, TimeOnly EndTime, IReadOnlyCollection<Guid> PeopleIds, Guid? RoomId,
    ClassSessionId? Except);

/// <summary>
/// Trùng lịch khi xếp 1 buổi chỉ là CẢNH BÁO (quy tắc vàng 3): GV / trợ giảng đang có buổi khác, phòng đang có buổi khác, ngày lễ.
/// Buổi khác = chưa hủy, cùng ngày, giờ giao nhau; GV / phòng hiệu lực (ghi đè của buổi hoặc theo lớp).
/// </summary>
public sealed class SessionConflicts(IAcademicDbContext db, SessionScheduler scheduler, IEmployeeDirectory employees)
{
    public async Task<IReadOnlyList<string>> WarningsAsync(SessionProbe p, CancellationToken ct)
    {
        var warnings = new List<string>();
        if ((await scheduler.HolidayMapAsync(ct)).TryGetValue(p.Date, out var holiday)) warnings.Add($"Ngày {p.Date:dd/MM/yyyy} là ngày lễ ({holiday})");

        var sameDay = await db.ClassSessions.AsNoTracking().AsSplitQuery()
            .Where(s => s.Date == p.Date && s.Status != SessionStatus.Cancelled && s.StartTime < p.EndTime && p.StartTime < s.EndTime)
            .ToListAsync(ct);
        sameDay = sameDay.Where(s => p.Except is null || s.Id != p.Except).ToList();
        if (sameDay.Count == 0) return warnings;

        var classIds = sameDay.Select(s => s.ClassId).Distinct().ToList();
        var classes = await db.Classes.AsNoTracking().IgnoreQueryFilters().Where(c => classIds.Contains(c.Id)).ToDictionaryAsync(c => c.Id, ct);
        var people = p.PeopleIds.ToHashSet();
        var names = people.Count == 0 ? new Dictionary<Guid, string>()
            : (await employees.FindManyAsync(people, ct)).ToDictionary(x => x.Key, x => x.Value.FullName);

        foreach (var s in sameDay)
        {
            var c = classes[s.ClassId];
            var time = $"{TimeText.Of(s.StartTime)}–{TimeText.Of(s.EndTime)}";
            var busy = SessionReader.Teachers(s, c).Concat(s.AssistantIds ?? c.AssistantIds).Distinct().Where(people.Contains);
            foreach (var person in busy)
                warnings.Add($"{names.GetValueOrDefault(person) ?? "Giáo viên"} đang có buổi lớp {c.Code} ({time})");
            if (p.RoomId is { } room && !c.Online && (s.RoomId ?? c.RoomId)?.Value == room)
                warnings.Add($"Phòng đang có buổi lớp {c.Code} ({time})");
        }
        return warnings;
    }

    /// <summary>GV + trợ giảng hiệu lực của buổi sau khi sửa</summary>
    public static IReadOnlyCollection<Guid> PeopleOf(ClassSession s, Class c) =>
        SessionReader.Teachers(s, c).Concat(s.AssistantIds ?? c.AssistantIds).Distinct().ToList();
}
