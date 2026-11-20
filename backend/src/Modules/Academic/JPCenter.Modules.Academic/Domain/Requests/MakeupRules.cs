using JPCenter.SharedKernel.Primitives;
using JPCenter.Modules.Academic.Domain.Classes;
using JPCenter.Modules.Academic.Domain.ClassSessions;

namespace JPCenter.Modules.Academic.Domain.Requests;

/// <summary>
/// Chọn buổi học bù ở lớp khác khi xin nghỉ (DT-18 bước 2): lớp khác cùng khóa, cùng bài, chưa diễn ra, trong 3 tuần tới.
/// Còn chỗ / không trùng giờ lớp HV đang học — handler kiểm (cần sĩ số + lịch của HV).
/// </summary>
public static class MakeupRules
{
    /// <summary>Cửa sổ chọn buổi bù tính từ hôm nay</summary>
    public const int WindowDays = 21;

    /// <param name="original">Buổi xin nghỉ</param>
    /// <param name="candidate">Buổi bù được chọn</param>
    /// <param name="sameCourse">Lớp của buổi bù cùng khóa với lớp gốc</param>
    /// <param name="now">Giờ VN</param>
    public static Result Check(ClassSession original, ClassSession candidate, bool sameCourse, DateTime now)
    {
        var today = DateOnly.FromDateTime(now);
        var ok = sameCourse
            && candidate.ClassId != original.ClassId
            && candidate.Status == SessionStatus.Scheduled && !candidate.IsConfirmed
            && candidate.StartsAt > now
            && candidate.Date <= today.AddDays(WindowDays)
            && (original.LessonId is null || candidate.LessonId == original.LessonId);
        return ok ? Result.Success() : RequestErrors.MakeupInvalid;
    }

    /// <summary>2 khung giờ cùng ngày có chồng nhau không (kiểm trùng giờ với lớp HV đang học)</summary>
    public static bool Overlaps(DateOnly date, TimeOnly start, TimeOnly end, DateOnly otherDate, TimeOnly otherStart, TimeOnly otherEnd) =>
        date == otherDate && start < otherEnd && otherStart < end;

    /// <summary>Lớp học bù còn chỗ: sĩ số + khách học bù đã nhận ở buổi đó &lt; sĩ số tối đa</summary>
    public static bool HasSeat(Class target, int enrolled, ClassSession candidate) =>
        enrolled + candidate.Attendance.Count(a => a.IsGuest) < target.MaxStudents;
}
