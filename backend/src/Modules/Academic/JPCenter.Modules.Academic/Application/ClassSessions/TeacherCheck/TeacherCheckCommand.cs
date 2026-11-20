using JPCenter.Application.Common.Messaging;
using JPCenter.Modules.Academic.Application.Abstractions;
using JPCenter.Modules.Academic.Application.ClassSessions.Common;
using JPCenter.SharedKernel.Domain;
using JPCenter.SharedKernel.Primitives;

namespace JPCenter.Modules.Academic.Application.ClassSessions.TeacherCheck;

/// <summary>
/// DT-12 bước 1 — GV / trợ giảng tự check-in (từ 30 phút trước giờ học; sau giờ bắt đầu → muộn) hoặc check-out (E2: chưa check-in → lỗi)
/// trên cổng GV. Vai trò suy từ buổi: GV lớp · dạy thay · trợ giảng.
/// </summary>
public sealed record TeacherCheckCommand(Guid Id, bool CheckOut) : ICommand<SessionResult>;

public sealed class TeacherCheckHandler(IAcademicDbContext db, TeacherSessions teacher, SessionReader reader, IClock clock)
    : ICommandHandler<TeacherCheckCommand, SessionResult>
{
    public async Task<Result<SessionResult>> Handle(TeacherCheckCommand cmd, CancellationToken ct)
    {
        var found = await teacher.ForUpdateAsync(cmd.Id, ct);
        if (found.IsFailure) return found.Error;
        var (session, cls) = found.Value;

        var r = cmd.CheckOut
            ? session.SelfCheckOut(teacher.Me, clock.LocalNow, clock.UtcNow)
            : session.SelfCheckIn(teacher.Me, TeacherSessions.RoleOf(session, cls, teacher.Me)!.Value, clock.LocalNow, clock.UtcNow);
        if (r.IsFailure) return r.Error;
        await db.SaveChangesAsync(ct);

        var warnings = new List<string>();
        if (!cmd.CheckOut && session.TeacherAttendance.First(t => t.TeacherId == teacher.Me).Status == Domain.ClassSessions.TeacherAttendanceStatus.Late)
            warnings.Add("Check-in sau giờ bắt đầu — ghi nhận đi muộn");
        return new SessionResult(await reader.ReloadAsync(session.Id, ct), warnings);
    }
}
