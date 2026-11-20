using JPCenter.Modules.Academic.Contracts;
using JPCenter.Modules.Students.Application.Abstractions;
using JPCenter.Modules.Students.Application.Common;
using JPCenter.Modules.Students.Domain.CareCases;
using Microsoft.EntityFrameworkCore;

namespace JPCenter.Modules.Students.Application.CareCases.Common;

/// <summary>Chuyên cần hiện tại (đọc trực tiếp từ Academic) để giáo vụ thấy vì sao HV bị cảnh báo</summary>
public sealed record CareStatsDto(int Taken, int Absent, int Streak, int? AttendanceRate);

/// <summary>
/// Trường hợp cảnh báo bỏ học (HV-14) — chỉ nhân viên xem (HV-14.E5). <c>noLongerFlagged</c> = hết dấu hiệu nhưng case còn mở (E2).
/// Frontend (<c>atRisk.ts › CareCase</c>) chỉ lưu người phụ trách / trạng thái / hẹn — phần còn lại trước đây tự tính ở trình duyệt.
/// </summary>
public sealed record CareCaseDto(
    Guid Id,
    Guid StudentId,
    string StudentCode,
    string StudentName,
    Guid ClassId,
    string ClassCode,
    Guid BranchId,
    RiskLevel RiskLevel,
    IReadOnlyList<string> Flags,
    bool NoLongerFlagged,
    Guid? AssigneeId,
    string? Assignee,
    CareStatus Status,
    DateOnly? NextAt,
    DateTimeOffset DetectedAt,
    DateTimeOffset? LastContactAt,
    CareStatsDto? Stats,
    DateTimeOffset UpdatedAt);

public sealed record CareLogDto(Guid Id, Guid StudentId, Guid ClassId, CareChannel Channel, string Note, Guid ById, string By, DateTimeOffset At);

public sealed class CareCaseReader(IStudentsDbContext db, IClassLookup classes, IAttendanceStats attendance, EmployeeNames employees)
{
    public async Task<IReadOnlyList<CareCaseDto>> ToDtosAsync(IReadOnlyList<CareCase> list, CancellationToken ct)
    {
        if (list.Count == 0) return [];
        var ids = list.Select(c => c.Id).ToList();
        var updated = await db.CareCases.AsNoTracking().IgnoreQueryFilters().Where(c => ids.Contains(c.Id))
            .Select(c => new { c.Id, At = EF.Property<DateTimeOffset?>(c, "UpdatedAt") ?? EF.Property<DateTimeOffset>(c, "CreatedAt") })
            .ToDictionaryAsync(x => x.Id, x => x.At, ct);
        var studentIds = list.Select(c => c.StudentId).Distinct().ToList();
        var students = await db.Students.AsNoTracking().IgnoreQueryFilters().Where(s => studentIds.Contains(s.Id))
            .ToDictionaryAsync(s => s.Id, s => new { s.Code, s.FullName }, ct);
        var classIds = list.Select(c => c.ClassId).Distinct().ToList();
        var classMap = await classes.FindManyAsync(classIds, ct);
        var stats = (await attendance.ForClassesAsync(classIds, studentIds.Select(s => s.Value).ToList(), ct))
            .ToDictionary(s => (s.ClassId, s.StudentId));
        var names = await employees.OfAsync(list.Where(c => c.AssigneeId is not null).Select(c => c.AssigneeId!.Value), ct);
        var lastContacts = await db.CareLogs.AsNoTracking()
            .Where(l => classIds.Contains(l.ClassId) && studentIds.Contains(l.StudentId))
            .GroupBy(l => new { l.ClassId, l.StudentId })
            .Select(g => new { g.Key.ClassId, g.Key.StudentId, At = g.Max(l => l.At) })
            .ToListAsync(ct);
        var last = lastContacts.ToDictionary(x => (x.ClassId, x.StudentId), x => x.At);

        return list.Select(c =>
        {
            var st = students.GetValueOrDefault(c.StudentId);
            var s = stats.GetValueOrDefault((c.ClassId, c.StudentId.Value));
            return new CareCaseDto(c.Id.Value, c.StudentId.Value, st?.Code ?? "—", st?.FullName ?? "—", c.ClassId,
                classMap.GetValueOrDefault(c.ClassId)?.Code ?? "—", c.BranchId.Value, c.RiskLevel, c.Flags, c.NoLongerFlagged, c.AssigneeId,
                c.AssigneeId is { } a ? names.GetValueOrDefault(a) : null, c.Status, c.NextAt, c.DetectedAt,
                last.TryGetValue((c.ClassId, c.StudentId), out var lc) ? lc : null,
                s is null ? null : new CareStatsDto(s.Taken, s.Absent, s.Streak, s.AttendanceRate), updated.GetValueOrDefault(c.Id, c.DetectedAt));
        })
        .OrderByDescending(d => d.RiskLevel).ThenBy(d => d.Status == CareStatus.Resolved).ThenByDescending(d => d.DetectedAt)
        .ToList();
    }

    public static CareLogDto LogDto(CareLog l) => new(l.Id.Value, l.StudentId.Value, l.ClassId, l.Channel, l.Note, l.By, l.ByName, l.At);
}
