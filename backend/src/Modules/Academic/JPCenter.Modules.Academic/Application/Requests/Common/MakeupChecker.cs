using JPCenter.Modules.Academic.Application.Abstractions;
using JPCenter.Modules.Academic.Application.Classes.Common;
using JPCenter.Modules.Academic.Application.Common;
using JPCenter.Modules.Academic.Domain.Classes;
using JPCenter.Modules.Academic.Domain.ClassSessions;
using JPCenter.Modules.Academic.Domain.Requests;
using JPCenter.Modules.Students.Contracts;
using JPCenter.SharedKernel.Domain;
using JPCenter.SharedKernel.Primitives;
using Microsoft.EntityFrameworkCore;

namespace JPCenter.Modules.Academic.Application.Requests.Common;

/// <summary>Buổi học bù HV chọn được (DT-18 bước 2)</summary>
public sealed record MakeupOptionDto(Guid SessionId, Guid ClassId, string ClassCode, string ClassName, int No, DateOnly Date, string StartTime,
    string EndTime, string? Room, bool Online, int SeatsLeft);

/// <summary>
/// DT-18 bước 2 — buổi học bù hợp lệ: <see cref="MakeupRules"/> (lớp khác cùng khóa, cùng bài, chưa diễn ra, ≤ 3 tuần) + lớp đích còn chỗ
/// (sĩ số + khách học bù &lt; tối đa) + không trùng giờ buổi khác HV đang học (trừ chính buổi xin nghỉ).
/// </summary>
public sealed class MakeupChecker(IAcademicDbContext db, ClassRoster roster, IEnrollmentLookup enrollments, IClock clock)
{
    public async Task<Result> CheckAsync(ClassSession original, Class originalClass, ClassSession candidate, Guid studentId, CancellationToken ct)
    {
        var target = await db.Classes.IgnoreQueryFilters().AsNoTracking().FirstAsync(c => c.Id == candidate.ClassId, ct);
        if (MakeupRules.Check(original, candidate, SameCourse(originalClass, target), clock.LocalNow) is { IsFailure: true } r) return r;
        if (!MakeupRules.HasSeat(target, await roster.EnrolledAsync(target.Id.Value, ct) ?? 0, candidate)) return RequestErrors.MakeupFull;
        return (await ClashesAsync(studentId, original.Id, [candidate], ct)).Contains(candidate.Id) ? RequestErrors.MakeupClash : Result.Success();
    }

    public async Task<IReadOnlyList<MakeupOptionDto>> OptionsAsync(ClassSession original, Class originalClass, Guid studentId, CancellationToken ct)
    {
        var today = clock.Today;
        var last = today.AddDays(MakeupRules.WindowDays);
        var classes = await db.Classes.AsNoTracking()
            .Where(c => c.CourseId == originalClass.CourseId && c.Id != originalClass.Id && (c.Status == ClassStatus.Enrolling || c.Status == ClassStatus.Ongoing))
            .ToDictionaryAsync(c => c.Id, ct);
        if (classes.Count == 0) return [];

        var classIds = classes.Keys.ToList();
        var now = clock.LocalNow;
        var candidates = (await db.ClassSessions.AsNoTracking()
                .Where(s => classIds.Contains(s.ClassId) && s.Date >= today && s.Date <= last && s.Status == SessionStatus.Scheduled)
                .ToListAsync(ct))
            .Where(s => MakeupRules.Check(original, s, true, now).IsSuccess)
            .ToList();
        if (candidates.Count == 0) return [];

        var enrolled = await roster.EnrolledAsync(classIds.Select(c => c.Value).ToList(), ct);
        var clashes = await ClashesAsync(studentId, original.Id, candidates, ct);
        var rooms = await db.Rooms.AsNoTracking().IgnoreQueryFilters().ToDictionaryAsync(r => r.Id, r => r.Name, ct);
        return candidates
            .Where(s => !clashes.Contains(s.Id) && MakeupRules.HasSeat(classes[s.ClassId], enrolled[s.ClassId.Value], s))
            .Select(s =>
            {
                var c = classes[s.ClassId];
                var room = (s.RoomId ?? c.RoomId) is { } rid ? rooms.GetValueOrDefault(rid) : null;
                var left = c.MaxStudents - enrolled[c.Id.Value] - s.Attendance.Count(a => a.IsGuest);
                return new MakeupOptionDto(s.Id.Value, c.Id.Value, c.Code, c.Name, s.No, s.Date, TimeText.Of(s.StartTime), TimeText.Of(s.EndTime), room,
                    c.Online, left);
            })
            .OrderBy(o => o.Date).ThenBy(o => o.StartTime, StringComparer.Ordinal)
            .ToList();
    }

    private static bool SameCourse(Class original, Class target) =>
        target.CourseId == original.CourseId && target.Status is ClassStatus.Enrolling or ClassStatus.Ongoing;

    /// <summary>Buổi ứng viên trùng giờ với buổi khác (chưa hủy) của các lớp HV đang học — trừ buổi xin nghỉ</summary>
    private async Task<HashSet<ClassSessionId>> ClashesAsync(Guid studentId, ClassSessionId except, IReadOnlyList<ClassSession> candidates,
        CancellationToken ct)
    {
        var mine = (await enrollments.OfStudentAsync(studentId, ct)).Where(e => e.Status is "studying" or "reserved")
            .Select(e => new ClassId(e.ClassId)).ToList();
        var dates = candidates.Select(c => c.Date).Distinct().ToList();
        var busy = await db.ClassSessions.AsNoTracking()
            .Where(s => mine.Contains(s.ClassId) && dates.Contains(s.Date) && s.Status != SessionStatus.Cancelled && s.Id != except)
            .Select(s => new { s.Date, s.StartTime, s.EndTime })
            .ToListAsync(ct);
        return candidates
            .Where(c => busy.Any(b => MakeupRules.Overlaps(c.Date, c.StartTime, c.EndTime, b.Date, b.StartTime, b.EndTime)))
            .Select(c => c.Id).ToHashSet();
    }
}
