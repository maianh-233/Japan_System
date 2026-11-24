using JPCenter.Infrastructure.Common.Persistence.Conversions;
using JPCenter.Modules.Academic.Contracts;
using JPCenter.Modules.Academic.Contracts.Dtos;
using JPCenter.Modules.Academic.Domain.ClassSessions;
using JPCenter.Modules.Academic.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;

namespace JPCenter.Modules.Academic.Infrastructure.Services;

/// <summary>
/// Hiện thực <see cref="ITeachingLog"/>: buổi chưa hủy → người có chấm công không vắng, đủ check-in / check-out; buổi hủy → người được phân công
/// (GV / trợ giảng của buổi, không có thì của lớp) để Finance đếm "buổi bị hủy". Chi nhánh = chi nhánh của lớp (TC-13.E5).
/// </summary>
internal sealed class TeachingLog(AcademicDbContext db) : ITeachingLog
{
    public async Task<IReadOnlyList<TaughtSession>> SessionsAsync(DateOnly from, DateOnly to, Guid? teacherId, CancellationToken ct)
    {
        var sessions = await db.ClassSessions.AsNoTracking().Where(s => s.Date >= from && s.Date <= to).ToListAsync(ct);
        var classIds = sessions.Select(s => s.ClassId).Distinct().ToList();
        var classes = await db.Classes.AsNoTracking().Where(c => classIds.Contains(c.Id)).ToDictionaryAsync(c => c.Id, ct);

        var result = new List<TaughtSession>();
        foreach (var s in sessions)
        {
            if (!classes.TryGetValue(s.ClassId, out var c)) continue;
            TaughtSession Row(Guid teacher, StaffRole role, TimeOnly? checkIn, TimeOnly? checkOut, int minutes, bool late, bool cancelled) => new(
                s.Id.Value, c.Id.Value, c.Code, c.Name, c.BranchId.Value, s.Date, s.StartTime, s.EndTime, teacher,
                SnakeCaseEnumConverter<StaffRole>.ToText(role), c.Online, checkIn, checkOut, minutes, late, s.IsConfirmed, cancelled);

            if (s.Status == SessionStatus.Cancelled)
            {
                var teachers = s.TeacherIds.Count > 0 ? s.TeacherIds : c.TeacherIds;
                var assistants = s.AssistantIds ?? c.AssistantIds;
                result.AddRange(teachers.Distinct().Select(t => Row(t, StaffRole.Teacher, null, null, 0, false, true)));
                result.AddRange(assistants.Except(teachers).Distinct().Select(t => Row(t, StaffRole.Assistant, null, null, 0, false, true)));
                continue;
            }
            foreach (var t in s.TeacherAttendance)
            {
                if (t.Status == TeacherAttendanceStatus.Absent || t.CheckIn is not { } i || t.CheckOut is not { } o) continue;
                var minutes = Math.Max(0, (int)(o.ToTimeSpan() - i.ToTimeSpan()).TotalMinutes);
                result.Add(Row(t.TeacherId, t.Role, i, o, minutes, t.Status == TeacherAttendanceStatus.Late, false));
            }
        }
        return result.Where(r => teacherId is null || r.TeacherId == teacherId)
            .OrderBy(r => r.Date).ThenBy(r => r.StartTime).ThenBy(r => r.ClassCode).ToList();
    }
}
