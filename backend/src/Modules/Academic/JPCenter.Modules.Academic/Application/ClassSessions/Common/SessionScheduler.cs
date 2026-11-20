using System.Security.Cryptography;
using JPCenter.Modules.Academic.Application.Abstractions;
using JPCenter.Modules.Academic.Application.Common;
using JPCenter.Modules.Academic.Domain.Classes;
using JPCenter.Modules.Academic.Domain.ClassSessions;
using JPCenter.Modules.Academic.Domain.Curricula;
using JPCenter.Modules.System.Contracts;
using JPCenter.SharedKernel.Domain;
using Microsoft.EntityFrameworkCore;

namespace JPCenter.Modules.Academic.Application.ClassSessions.Common;

/// <summary>
/// Tùy chọn sinh lịch (DT-08). Bỏ trống = theo lớp: từ ngày sau buổi đã dạy cuối (không trước hôm nay nếu lớp đã khai giảng),
/// thứ + giờ của lớp, số buổi = số buổi khóa − số buổi đã dạy / còn giữ.
/// </summary>
public sealed record ScheduleOptions(DateOnly? From, List<int>? Weekdays, TimeOnly? StartTime, TimeOnly? EndTime, int? Count, bool SkipHolidays = true);

/// <summary>1 buổi dự kiến trong bản xem trước</summary>
public sealed record PlannedSessionDto(int No, DateOnly Date, string StartTime, string EndTime, SessionKind Kind, Guid? LessonId, Guid? ExamId);

/// <summary>Ngày bị bỏ vì nghỉ lễ</summary>
public sealed record SkippedDayDto(DateOnly Date, string Name);

/// <summary>
/// Xem trước lịch mới: <c>Kept</c> buổi giữ nguyên (đã diễn ra / đã điểm danh / hủy / học bù / học thêm),
/// <c>Replaced</c> buổi chưa dạy bị thay, <c>OverridesLost</c> trong số đó có thay đổi riêng (DT-08.E3).
/// </summary>
public sealed record SchedulePreviewDto(
    DateOnly From,
    int Count,
    DateOnly? EndDate,
    int ExamCount,
    int Kept,
    int Replaced,
    int OverridesLost,
    bool HasCurriculum,
    IReadOnlyList<SkippedDayDto> Skipped,
    IReadOnlyList<PlannedSessionDto> Sessions);

/// <summary>Sinh lịch buổi theo lịch tuần + lộ trình (<see cref="SessionPlanner"/>), bỏ ngày lễ từ Cài đặt chung (DT-08.E5)</summary>
public sealed class SessionScheduler(IAcademicDbContext db, IHolidayCalendar holidays, IClock clock)
{
    public async Task<SchedulePreviewDto> PreviewAsync(Class cls, ScheduleOptions o, CancellationToken ct)
    {
        var existing = await db.ClassSessions.AsNoTracking().AsSplitQuery().Where(s => s.ClassId == cls.Id).ToListAsync(ct);
        return (await PlanAsync(cls, existing, o, ct)).Preview;
    }

    /// <summary>
    /// Thay buổi chưa dạy bằng lịch mới (chưa lưu — handler gọi SaveChanges). Cập nhật ngày kết thúc dự kiến của lớp
    /// khi lớp chưa kết thúc. Trả bản xem trước của lịch đã áp.
    /// </summary>
    public async Task<SchedulePreviewDto> ApplyAsync(Class cls, ScheduleOptions o, CancellationToken ct)
    {
        var existing = await db.ClassSessions.AsSplitQuery().Where(s => s.ClassId == cls.Id).ToListAsync(ct);
        var plan = await PlanAsync(cls, existing, o, ct);
        db.ClassSessions.RemoveRange(plan.Replaceable);
        foreach (var p in plan.Sessions)
        {
            var created = ClassSession.Create(cls.Id, cls.BranchId, p.No, p.Date, plan.Start, plan.End, p.Slot.Kind, p.Slot.LessonId,
                p.Slot.ExamId, SessionOrigin.Regular, null, NewQrToken());
            db.ClassSessions.Add(created.Value);
        }
        if (plan.Preview.EndDate is { } end && !cls.IsClosed) cls.SetPlannedEnd(end);
        return plan.Preview;
    }

    /// <summary>Ngày kết thúc dự kiến khi mở lớp (chưa có buổi nào)</summary>
    public async Task<DateOnly?> EndDateAsync(DateOnly from, IReadOnlyCollection<int> weekdays, int count, CancellationToken ct)
    {
        var plan = SessionPlanner.Plan(from, weekdays, count, 1, 0, await HolidayMapAsync(ct), true, [], []);
        return plan.EndDate;
    }

    public async Task<IReadOnlyDictionary<DateOnly, string>> HolidayMapAsync(CancellationToken ct) =>
        (await holidays.ListAsync(ct)).GroupBy(h => h.Date).ToDictionary(g => g.Key, g => g.First().Name);

    /// <summary>Mã QR điểm danh cố định của buổi (giống dữ liệu mẫu)</summary>
    public static string NewQrToken() => Convert.ToHexString(RandomNumberGenerator.GetBytes(16)).ToLowerInvariant();

    private async Task<Plan> PlanAsync(Class cls, IReadOnlyList<ClassSession> existing, ScheduleOptions o, CancellationToken ct)
    {
        var now = clock.LocalNow;
        var replaceable = existing.Where(s => s.IsReplaceable(now)).ToList();
        var kept = existing.Except(replaceable).ToList();
        var series = kept.Where(s => s.Origin != SessionOrigin.Extra).ToList();
        var doneSlots = series.Count(s => s.Status != SessionStatus.Cancelled);
        var startNo = series.Count == 0 ? 1 : series.Max(s => s.No) + 1;

        var from = o.From ?? DefaultFrom(cls, series);
        var weekdays = o.Weekdays is { Count: > 0 } w ? w : cls.Weekdays;
        var start = o.StartTime ?? cls.StartTime;
        var end = o.EndTime ?? cls.EndTime;
        var count = Math.Max(0, o.Count ?? cls.SessionCount - doneSlots);

        var (lessons, exams) = await ContentAsync(cls.CurriculumId, ct);
        var plan = SessionPlanner.Plan(from, weekdays, count, startNo, doneSlots, await HolidayMapAsync(ct), o.SkipHolidays, lessons, exams);
        var preview = new SchedulePreviewDto(from, plan.Sessions.Count, plan.EndDate, plan.Sessions.Count(s => s.Slot.Kind == SessionKind.Exam),
            kept.Count, replaceable.Count, replaceable.Count(s => s.HasOverrides), lessons.Count > 0,
            plan.Skipped.Select(s => new SkippedDayDto(s.Date, s.Name)).ToList(),
            plan.Sessions.Select(s => new PlannedSessionDto(s.No, s.Date, TimeText.Of(start), TimeText.Of(end), s.Slot.Kind,
                s.Slot.LessonId?.Value, s.Slot.ExamId?.Value)).ToList());
        return new Plan(preview, plan.Sessions, replaceable, start, end);
    }

    private DateOnly DefaultFrom(Class cls, IReadOnlyList<ClassSession> series)
    {
        var start = cls.ActualStartDate ?? cls.PlannedStartDate;
        var after = series.Count == 0 ? start : series.Max(s => s.Date).AddDays(1);
        if (after < start) after = start;
        return cls.Status != ClassStatus.Enrolling && after < clock.Today ? clock.Today : after;
    }

    private async Task<(IReadOnlyList<LessonId> Lessons, IReadOnlyList<(ExamMilestoneId, LessonId)> Exams)> ContentAsync(
        CurriculumId? id, CancellationToken ct)
    {
        if (id is null) return ([], []);
        var cur = await db.Curricula.AsNoTracking().IgnoreQueryFilters().FirstOrDefaultAsync(c => c.Id == id, ct);
        if (cur is null) return ([], []);
        return (cur.Lessons.OrderBy(l => l.Order).Select(l => l.Id).ToList(), cur.Exams.Select(e => (e.Id, e.AfterLessonId)).ToList());
    }

    private sealed record Plan(SchedulePreviewDto Preview, IReadOnlyList<PlannedSession> Sessions, IReadOnlyList<ClassSession> Replaceable,
        TimeOnly Start, TimeOnly End);
}
