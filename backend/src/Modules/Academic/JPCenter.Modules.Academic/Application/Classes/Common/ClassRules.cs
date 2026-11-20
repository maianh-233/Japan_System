using JPCenter.Modules.Academic.Application.Abstractions;
using JPCenter.Modules.Academic.Domain.Classes;
using JPCenter.Modules.Academic.Domain.Courses;
using JPCenter.Modules.Academic.Domain.Curricula;
using JPCenter.Modules.Academic.Domain.Rooms;
using JPCenter.Modules.Hr.Contracts;
using JPCenter.Modules.System.Contracts;
using JPCenter.SharedKernel.Primitives;
using Microsoft.EntityFrameworkCore;

namespace JPCenter.Modules.Academic.Application.Classes.Common;

/// <summary>Lịch tuần dài hạn của 1 lớp — để dò trùng GV / phòng khi mở / sửa lớp (DT-02.E8)</summary>
public sealed record ClassScheduleProbe(
    IReadOnlyCollection<Guid> PeopleIds,
    Guid? RoomId,
    DateOnly From,
    DateOnly To,
    IReadOnlyCollection<int> Weekdays,
    TimeOnly StartTime,
    TimeOnly EndTime);

/// <summary>Quy tắc mở / sửa lớp cần dữ liệu ngoài aggregate (DT-02.E1, E8, E9, E10)</summary>
public sealed class ClassRules(IAcademicDbContext db, IBranchDirectory branches, IEmployeeDirectory employees)
{
    public static readonly Error BranchInactive = Error.Validation("system.branch.inactive", "Chỉ chọn chi nhánh đang hoạt động / đang chuẩn bị");
    public static readonly Error CurriculumInvalid = Error.Validation("academic.class.invalid_curriculum",
        "Lộ trình phải gắn với khóa và đã phát hành");

    public async Task<Result> EnsureCodeUniqueAsync(string normalizedCode, ClassId? except, CancellationToken ct)
    {
        var query = db.Classes.Where(c => c.Code == normalizedCode);
        if (except is { } id) query = query.Where(c => c.Id != id);
        return await query.AnyAsync(ct) ? ClassErrors.DuplicateCode(normalizedCode) : Result.Success();
    }

    /// <summary>
    /// Khóa của lớp: đang bán (active) ở chi nhánh (DT-02.E10). Sửa lớp mà giữ nguyên khóa + chi nhánh → không kiểm lại
    /// (khóa tạm ngừng không ảnh hưởng lớp đã mở).
    /// </summary>
    public async Task<Result<Course>> CourseAsync(Guid courseId, Guid branchId, Class? current, CancellationToken ct)
    {
        var course = await db.Courses.AsNoTracking().FirstOrDefaultAsync(c => c.Id == new CourseId(courseId), ct);
        if (course is null) return CourseErrors.NotFound;
        var unchanged = current is not null && current.CourseId == course.Id && current.BranchId.Value == branchId;
        if (!unchanged && (course.Status != CourseStatus.Active || !course.BranchIds.Contains(new BranchId(branchId))))
            return ClassErrors.CourseNotAvailable;
        return course;
    }

    /// <summary>Lộ trình gắn với khóa và đã phát hành (giữ nguyên lộ trình cũ của lớp thì không kiểm)</summary>
    public async Task<Result> EnsureCurriculumAsync(Guid? curriculumId, Course course, Class? current, CancellationToken ct)
    {
        if (curriculumId is not { } id || current?.CurriculumId?.Value == id) return Result.Success();
        if (course.Curricula.All(l => l.CurriculumId.Value != id)) return CurriculumInvalid;
        var published = await db.Curricula.AnyAsync(c => c.Id == new CurriculumId(id) && c.Status == CurriculumStatus.Published, ct);
        return published ? Result.Success() : CurriculumInvalid;
    }

    public async Task<Result> EnsureBranchAsync(Guid branchId, Class? current, CancellationToken ct)
    {
        if (current?.BranchId.Value == branchId) return Result.Success();
        return await branches.FindAsync(branchId, ct) is { AcceptsNewData: true } ? Result.Success() : BranchInactive;
    }

    /// <summary>GV / trợ giảng phải là người dạy chưa nghỉ việc (DT-02.E9). Người đã có trong lớp được giữ nguyên.</summary>
    public async Task<Result> EnsureStaffAsync(IEnumerable<Guid> ids, Class? current, CancellationToken ct)
    {
        var existing = current is null ? [] : current.TeacherIds.Concat(current.AssistantIds).ToHashSet();
        var wanted = ids.Distinct().Where(i => !existing.Contains(i)).ToList();
        if (wanted.Count == 0) return Result.Success();
        var found = await employees.FindManyAsync(wanted, ct);
        return wanted.All(i => found.TryGetValue(i, out var e) && e.IsTeacher && !e.HasQuit) ? Result.Success() : ClassErrors.InvalidTeacher;
    }

    /// <summary>
    /// Phòng của lớp offline: thuộc chi nhánh, đang dùng được (DT-02.E9). Trả sức chứa để kiểm sĩ số max.
    /// Giữ nguyên phòng cũ của lớp → không chặn khi phòng đang bảo trì.
    /// </summary>
    public async Task<Result<int?>> RoomCapacityAsync(Guid? roomId, Guid branchId, bool online, Class? current, CancellationToken ct)
    {
        if (online || roomId is not { } id) return (int?)null;
        var room = await db.Rooms.AsNoTracking().FirstOrDefaultAsync(r => r.Id == new RoomId(id), ct);
        if (room is null) return RoomErrors.NotFound;
        if (room.BranchId.Value != branchId) return RoomErrors.WrongBranch;
        if (!room.IsUsable && current?.RoomId?.Value != id) return RoomErrors.Unavailable;
        return room.Capacity;
    }

    /// <summary>Lớp đang mở khác trùng thứ + giờ trong khoảng ngày, có chung GV / trợ giảng hoặc chung phòng</summary>
    public async Task<IReadOnlyList<ScheduleConflictDto>> ConflictsAsync(ClassScheduleProbe p, ClassId? except, CancellationToken ct)
    {
        var others = await db.Classes.AsNoTracking()
            .Where(c => c.Status == ClassStatus.Enrolling || c.Status == ClassStatus.Ongoing || c.Status == ClassStatus.Paused)
            .ToListAsync(ct);
        var people = p.PeopleIds.ToHashSet();
        var names = people.Count == 0 ? new Dictionary<Guid, string>()
            : (await employees.FindManyAsync(people, ct)).ToDictionary(x => x.Key, x => x.Value.FullName);
        var result = new List<ScheduleConflictDto>();
        foreach (var c in others.Where(c => except is null || c.Id != except))
        {
            var from = c.ActualStartDate ?? c.PlannedStartDate;
            if (from > p.To || c.PlannedEndDate < p.From) continue;
            if (!c.Weekdays.Intersect(p.Weekdays).Any() || c.StartTime >= p.EndTime || p.StartTime >= c.EndTime) continue;
            var time = $"{string.Join(", ", c.Weekdays.Intersect(p.Weekdays).Order().Select(WeekdayText))} {c.StartTime:HH\\:mm}–{c.EndTime:HH\\:mm}";
            foreach (var person in c.TeacherIds.Concat(c.AssistantIds).Distinct().Where(people.Contains))
                result.Add(new ScheduleConflictDto(c.Id.Value, c.Code, "teacher", person,
                    $"{names.GetValueOrDefault(person) ?? "Giáo viên"} đang dạy lớp {c.Code} ({time})"));
            if (p.RoomId is { } room && c.RoomId?.Value == room)
                result.Add(new ScheduleConflictDto(c.Id.Value, c.Code, "room", null, $"Phòng đang dùng cho lớp {c.Code} ({time})"));
        }
        return result;
    }

    public static string WeekdayText(int d) => d == 7 ? "CN" : $"T{d + 1}";
}
