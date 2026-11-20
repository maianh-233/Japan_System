using JPCenter.Modules.Academic.Application.Abstractions;
using JPCenter.Modules.Academic.Application.Portals.Common;
using JPCenter.Modules.Academic.Domain.Classes;
using JPCenter.Modules.Academic.Domain.ClassSessions;
using JPCenter.SharedKernel.Primitives;
using Microsoft.EntityFrameworkCore;

namespace JPCenter.Modules.Academic.Application.ClassSessions.Common;

/// <summary>
/// Cổng GV (DT-09, 12, 14, DT-20.E1): buổi GV đang gọi có tham gia — GV của lớp / GV ghi đè ở buổi (dạy thay) / trợ giảng / có chấm công.
/// Không tham gia → 403 <c>academic.teacher_attendance.not_assigned</c> (DT-12.E1).
/// </summary>
public sealed class TeacherSessions(IAcademicDbContext db, PortalOwner owner)
{
    public Guid Me => owner.TeacherId ?? Guid.Empty;

    /// <summary>Buổi (đang theo dõi để sửa) + lớp, nếu GV đang gọi có tham gia</summary>
    public async Task<Result<(ClassSession Session, Class Class)>> ForUpdateAsync(Guid sessionId, CancellationToken ct)
    {
        var s = await db.ClassSessions.FirstOrDefaultAsync(x => x.Id == new ClassSessionId(sessionId), ct);
        if (s is null) return SessionErrors.NotFound;
        var c = await db.Classes.IgnoreQueryFilters().FirstAsync(x => x.Id == s.ClassId, ct);
        return RoleOf(s, c, Me) is null ? SessionErrors.NotAssigned : (s, c);
    }

    /// <summary>Vai trò của GV trong buổi: GV lớp · dạy thay (GV ghi đè không phải GV lớp) · trợ giảng; null = không tham gia</summary>
    public static StaffRole? RoleOf(ClassSession s, Class c, Guid teacherId)
    {
        if (teacherId == Guid.Empty) return null;
        if (s.TeacherIds.Count > 0 && s.TeacherIds.Contains(teacherId))
            return c.TeacherIds.Contains(teacherId) ? StaffRole.Teacher : StaffRole.Substitute;
        if (s.TeacherIds.Count == 0 && c.TeacherIds.Contains(teacherId)) return StaffRole.Teacher;
        if ((s.AssistantIds ?? c.AssistantIds).Contains(teacherId)) return StaffRole.Assistant;
        return s.TeacherAttendance.FirstOrDefault(t => t.TeacherId == teacherId)?.Role;
    }

    /// <summary>Người đứng lớp hiệu lực của buổi (GV + trợ giảng) — đẩy realtime khi HV quét QR</summary>
    public static IReadOnlyList<Guid> StaffOf(ClassSession s, Class c) =>
        [.. SessionReader.Teachers(s, c), .. s.AssistantIds ?? c.AssistantIds];
}
