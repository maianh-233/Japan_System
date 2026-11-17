using JPCenter.Modules.Academic.Domain.Curricula;

namespace JPCenter.Modules.Academic.Domain.ClassSessions;

/// <summary>Nội dung 1 buổi theo lộ trình: học bài nào / thi mốc nào (không có lộ trình → buổi học chưa gán bài)</summary>
public sealed record PlannedSlot(SessionKind Kind, LessonId? LessonId, ExamMilestoneId? ExamId);

/// <summary>1 buổi dự kiến sinh ra</summary>
public sealed record PlannedSession(int No, DateOnly Date, PlannedSlot Slot);

/// <summary>Ngày bị bỏ vì nghỉ lễ</summary>
public sealed record SkippedDay(DateOnly Date, string Name);

public sealed record SessionPlan(IReadOnlyList<PlannedSession> Sessions, IReadOnlyList<SkippedDay> Skipped)
{
    public DateOnly? EndDate => Sessions.Count == 0 ? null : Sessions[^1].Date;
}

/// <summary>
/// Sinh lịch buổi học (DT-08) — thuần, không đụng DB, giống <c>frontend/.../sessionGenerator.ts</c>:
/// mỗi bài ~ (số buổi học ÷ số bài) buổi, buổi thi chèn ngay sau bài của mốc thi; bỏ ngày lễ.
/// </summary>
public static class SessionPlanner
{
    /// <summary>Giới hạn vòng lặp ngày — tránh treo khi lịch không hợp lệ (~ 10 năm)</summary>
    private const int MaxDays = 3660;

    /// <param name="lessons">Bài học theo thứ tự lộ trình</param>
    /// <param name="exams">Mốc thi: (mốc, sau bài nào) theo thứ tự lộ trình</param>
    public static IReadOnlyList<PlannedSlot> BuildSlots(int count, IReadOnlyList<LessonId> lessons,
        IReadOnlyList<(ExamMilestoneId Exam, LessonId AfterLesson)> exams)
    {
        if (count <= 0) return [];
        if (lessons.Count == 0) return [.. Enumerable.Repeat(new PlannedSlot(SessionKind.Lesson, null, null), count)];

        var examCount = Math.Min(exams.Count, Math.Max(0, count - lessons.Count));
        var lessonSlots = Math.Max(lessons.Count, count - examCount);
        var perLesson = lessonSlots / lessons.Count;
        var extra = lessonSlots % lessons.Count;
        var slots = new List<PlannedSlot>(lessonSlots + exams.Count);
        foreach (var lesson in lessons)
        {
            var n = perLesson + (extra-- > 0 ? 1 : 0);
            for (var i = 0; i < n; i++) slots.Add(new PlannedSlot(SessionKind.Lesson, lesson, null));
            foreach (var (exam, _) in exams.Where(e => e.AfterLesson == lesson))
                slots.Add(new PlannedSlot(SessionKind.Exam, null, exam));
        }
        return [.. slots.Take(count)];
    }

    /// <param name="from">Sinh từ ngày này (gồm cả ngày này)</param>
    /// <param name="weekdays">1 = Thứ 2 … 7 = Chủ nhật</param>
    /// <param name="count">Số buổi cần sinh</param>
    /// <param name="startNo">Đánh số từ (buổi đã dạy + 1)</param>
    /// <param name="skipSlots">Bỏ N buổi đầu của chuỗi bài / thi (đã dạy)</param>
    /// <param name="holidays">Ngày lễ → tên lễ</param>
    public static SessionPlan Plan(DateOnly from, IReadOnlyCollection<int> weekdays, int count, int startNo, int skipSlots,
        IReadOnlyDictionary<DateOnly, string> holidays, bool skipHolidays, IReadOnlyList<LessonId> lessons,
        IReadOnlyList<(ExamMilestoneId Exam, LessonId AfterLesson)> exams)
    {
        var sessions = new List<PlannedSession>();
        var skipped = new List<SkippedDay>();
        if (count <= 0 || weekdays.Count == 0 || weekdays.Any(d => d is < 1 or > 7)) return new SessionPlan(sessions, skipped);

        skipSlots = Math.Max(0, skipSlots);
        var slots = BuildSlots(count + skipSlots, lessons, exams).Skip(skipSlots).ToList();
        var day = from;
        for (var guard = 0; sessions.Count < count && guard < MaxDays; guard++, day = day.AddDays(1))
        {
            if (!weekdays.Contains(WeekdayOf(day))) continue;
            if (skipHolidays && holidays.TryGetValue(day, out var name))
            {
                skipped.Add(new SkippedDay(day, name));
                continue;
            }
            var slot = sessions.Count < slots.Count ? slots[sessions.Count] : new PlannedSlot(SessionKind.Lesson, null, null);
            sessions.Add(new PlannedSession(startNo + sessions.Count, day, slot));
        }
        return new SessionPlan(sessions, skipped);
    }

    /// <summary>1 = Thứ 2 … 7 = Chủ nhật</summary>
    public static int WeekdayOf(DateOnly date) => date.DayOfWeek == DayOfWeek.Sunday ? 7 : (int)date.DayOfWeek;
}
