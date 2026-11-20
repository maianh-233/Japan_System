using FluentValidation;
using JPCenter.Application.Common.Auditing;
using JPCenter.Application.Common.IntegrationEvents;
using JPCenter.Application.Common.Messaging;
using JPCenter.Application.Common.Security;
using JPCenter.Modules.Academic.Application.Abstractions;
using JPCenter.Modules.Academic.Application.ClassSessions.Common;
using JPCenter.Modules.Academic.Application.Common;
using JPCenter.Modules.Academic.Domain.Classes;
using JPCenter.Modules.Academic.Domain.ClassSessions;
using JPCenter.Modules.Students.Contracts;
using JPCenter.SharedKernel.Domain;
using JPCenter.SharedKernel.Primitives;
using Microsoft.EntityFrameworkCore;
using Ev = JPCenter.Modules.Academic.Contracts.IntegrationEvents;

namespace JPCenter.Modules.Academic.Application.ClassSessions.RecordAttendance;

/// <summary>
/// DT-09 bước 3–5 — điểm danh bấm tay cả danh sách (GV trên cổng: <paramref name="AsTeacher"/> = true; nhân viên điểm hộ: false).
/// HV ngoài danh sách lớp / khách học bù → <c>academic.attendance.not_in_roster</c>. GV không sửa sau khi buổi đã xác nhận (E6).
/// Trả cảnh báo vắng 2 buổi liên tiếp (E5); phát <c>AttendanceRecorded</c> để Students tính lại nguy cơ bỏ học.
/// </summary>
public sealed record RecordAttendanceCommand(Guid Id, IReadOnlyList<AttendanceEntry> Entries, bool AsTeacher) : ICommand<SessionResult>;

public sealed class RecordAttendanceValidator : AbstractValidator<RecordAttendanceCommand>
{
    public RecordAttendanceValidator()
    {
        RuleFor(x => x.Entries).NotEmpty().WithMessage("Chưa có học viên nào để điểm danh").Must(e => e.Count <= 200);
        RuleForEach(x => x.Entries).ChildRules(e =>
        {
            e.RuleFor(x => x.StudentId).NotEmpty();
            e.RuleFor(x => x.Status).IsInEnum();
            e.RuleFor(x => x.Note).MaximumLength(500);
            e.RuleFor(x => x.LeaveReason).MaximumLength(500);
        });
        RuleFor(x => x.Entries).Must(e => e.Select(x => x.StudentId).Distinct().Count() == e.Count).WithMessage("Học viên bị lặp trong danh sách");
    }
}

public sealed class RecordAttendanceHandler(IAcademicDbContext db, SessionReader reader, TeacherSessions teacher, AcademicAccess access,
    IEnrollmentLookup enrollments, MakeupSync makeups, IClock clock, IIntegrationEventPublisher events, IAuditTrail audit)
    : ICommandHandler<RecordAttendanceCommand, SessionResult>
{
    /// <summary>Vắng không phép liên tiếp từ N buổi → cảnh báo giáo vụ (DT-09.E5)</summary>
    public const int StreakWarnFrom = 2;

    public async Task<Result<SessionResult>> Handle(RecordAttendanceCommand cmd, CancellationToken ct)
    {
        ClassSession session;
        Class cls;
        if (cmd.AsTeacher)
        {
            var found = await teacher.ForUpdateAsync(cmd.Id, ct);
            if (found.IsFailure) return found.Error;
            (session, cls) = found.Value;
        }
        else if (await reader.ForUpdateAsync(cmd.Id, PermissionAction.Update, ct) is var (s, c)) (session, cls) = (s, c);
        else return SessionErrors.NotFound;

        // Danh sách hợp lệ = ghi danh hiệu lực + HV đã có dòng điểm danh (khách học bù, HV đã rời lớp sau khi được điểm danh)
        var roster = (await enrollments.RosterAsync(cls.Id.Value, ct)).Select(r => r.StudentId).ToHashSet();
        roster.UnionWith(session.Attendance.Select(a => a.StudentId));
        if (cmd.Entries.FirstOrDefault(e => !roster.Contains(e.StudentId)) is not null) return SessionErrors.NotInRoster;

        var wasConfirmed = session.IsConfirmed;
        var method = cmd.AsTeacher ? AttendanceMethod.Teacher : AttendanceMethod.Staff;
        var actor = cmd.AsTeacher ? teacher.Me : access.ActorId;
        if (session.RecordAttendance(cmd.Entries, method, actor, clock.UtcNow) is { IsFailure: true } r) return r.Error;

        var studentIds = cmd.Entries.Select(e => e.StudentId).ToList();
        await makeups.ApplyAsync(session, studentIds, ct);
        if (!cmd.AsTeacher)
            session.AddSupport(SupportKind.Attendance, $"Điểm danh hộ {cmd.Entries.Count} học viên", actor, clock.UtcNow);

        var warnings = await StreakWarningsAsync(session, ct);
        events.Publish(new Ev.AttendanceRecorded(session.Id.Value, cls.Id.Value, session.BranchId.Value, session.Date, studentIds));
        await db.SaveChangesAsync(ct);

        if (wasConfirmed)
            audit.Record(AuditCategory.Data, AuditSeverity.Warning, "Sửa điểm danh buổi đã xác nhận", $"{cls.Code} · buổi {session.No}",
                $"{cmd.Entries.Count} học viên");
        return new SessionResult(await reader.ReloadAsync(session.Id, ct), warnings);
    }

    /// <summary>HV đang vắng buổi này và đã vắng không phép ≥ 2 buổi liên tiếp (tính cả buổi này)</summary>
    private async Task<IReadOnlyList<string>> StreakWarningsAsync(ClassSession session, CancellationToken ct)
    {
        var absent = session.Attendance.Where(a => !a.IsGuest && a.Status == AttendanceStatus.Absent).Select(a => a.StudentId).ToList();
        if (absent.Count == 0) return [];
        var others = await db.ClassSessions.AsNoTracking()
            .Where(s => s.ClassId == session.ClassId && s.Id != session.Id && s.Status != SessionStatus.Cancelled && s.Date <= session.Date)
            .ToListAsync(ct);
        others.Add(session);
        var streaks = absent.Count(id => AttendanceStatsCalculator.Compute(session.ClassId.Value, id, others).Streak >= StreakWarnFrom);
        return streaks == 0 ? [] : [$"{streaks} HV vắng {StreakWarnFrom} buổi liên tiếp — nhắc giáo vụ liên hệ"];
    }
}
