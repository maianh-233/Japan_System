using FluentValidation;
using JPCenter.Application.Common.Auditing;
using JPCenter.Application.Common.IntegrationEvents;
using JPCenter.Application.Common.Messaging;
using JPCenter.Application.Common.Security;
using JPCenter.Modules.Academic.Application.Abstractions;
using JPCenter.Modules.Academic.Application.Classes.Common;
using JPCenter.Modules.Academic.Application.ClassSessions.Common;
using JPCenter.Modules.Academic.Application.Common;
using JPCenter.Modules.Academic.Domain.Classes;
using JPCenter.Modules.Academic.Domain.ClassSessions;
using JPCenter.Modules.Academic.Domain.Rooms;
using JPCenter.SharedKernel.Domain;
using JPCenter.SharedKernel.Primitives;
using Microsoft.EntityFrameworkCore;
using Ev = JPCenter.Modules.Academic.Contracts.IntegrationEvents;
namespace JPCenter.Modules.Academic.Application.ClassSessions.CreateSession;

/// <summary>
/// DT-10 — thêm buổi học bù (thay buổi đã hủy, tính vào số buổi khóa — mang bài / mốc thi của buổi gốc) hoặc học thêm
/// (ngoài số buổi, không bị "Tạo lịch tự động" xóa; <paramref name="LessonId"/> = bài dạy thêm). Trùng lịch chỉ cảnh báo.
/// </summary>
public sealed record CreateSessionCommand(Guid ClassId, SessionOrigin Origin, Guid? MakeupForId, DateOnly Date, TimeOnly StartTime,
    TimeOnly EndTime, List<Guid>? TeacherIds, Guid? RoomId, string? Note, Guid? LessonId = null) : ICommand<SessionResult>;

public sealed class CreateSessionValidator : AbstractValidator<CreateSessionCommand>
{
    public CreateSessionValidator()
    {
        RuleFor(x => x.Origin).NotEqual(SessionOrigin.Regular).WithMessage("Chỉ thêm buổi học bù / học thêm — buổi thường dùng \"Tạo lịch tự động\"");
        RuleFor(x => x.Note).MaximumLength(500);
    }
}

public sealed class CreateSessionHandler(IAcademicDbContext db, ClassReader classes, ClassRules rules, SessionReader reader,
    SessionConflicts conflicts, IAuditTrail audit) : ICommandHandler<CreateSessionCommand, SessionResult>
{
    public async Task<Result<SessionResult>> Handle(CreateSessionCommand cmd, CancellationToken ct)
    {
        var cls = await classes.ForUpdateAsync(cmd.ClassId, AcademicAccess.Sessions, PermissionAction.Create, ct);
        if (cls is null) return ClassErrors.NotFound;
        if (cls.IsClosed) return ClassErrors.Closed;

        ClassSession? original = null;
        if (cmd.Origin == SessionOrigin.Makeup)
        {
            if (cmd.MakeupForId is not { } fid) return SessionErrors.MakeupForRequired;
            original = await db.ClassSessions.AsNoTracking().FirstOrDefaultAsync(s => s.Id == new ClassSessionId(fid), ct);
            if (original is null || original.ClassId != cls.Id || original.Status != SessionStatus.Cancelled) return SessionErrors.MakeupTargetInvalid;
            if (await db.ClassSessions.AnyAsync(s => s.MakeupForId == original.Id && s.Status != SessionStatus.Cancelled, ct))
                return SessionErrors.AlreadyMadeUp;
        }
        var teachers = Normalize(cmd.TeacherIds, cls.TeacherIds);
        if (await rules.EnsureStaffAsync(teachers, cls, ct) is { IsFailure: true } staff) return staff.Error;
        var room = cls.Online || cmd.RoomId is not { } rid || rid == cls.RoomId?.Value ? (RoomId?)null : new RoomId(rid);
        if (room is { } r && await rules.RoomCapacityAsync(r.Value, cls.BranchId.Value, false, null, ct) is { IsFailure: true } bad) return bad.Error;

        var created = ClassSession.Create(cls.Id, cls.BranchId, original?.No ?? 0, cmd.Date, cmd.StartTime, cmd.EndTime,
            original?.Kind ?? SessionKind.Lesson, original?.LessonId ?? (cmd.LessonId is { } l ? new Domain.Curricula.LessonId(l) : null), original?.ExamId, cmd.Origin, original?.Id, SessionScheduler.NewQrToken());
        if (created.IsFailure) return created.Error;
        var session = created.Value;
        if (teachers.Count > 0 || room is not null)
            session.Edit(cmd.Date, cmd.StartTime, cmd.EndTime, teachers, null, room, cmd.Note ?? (cmd.Origin == SessionOrigin.Makeup ? "Học bù" : "Học thêm"));

        var warnings = await conflicts.WarningsAsync(new SessionProbe(cmd.Date, cmd.StartTime, cmd.EndTime,
            SessionConflicts.PeopleOf(session, cls), cls.Online ? null : (room ?? cls.RoomId)?.Value, null), ct);
        db.ClassSessions.Add(session);
        await db.SaveChangesAsync(ct);
        audit.Record(AuditCategory.Data, AuditSeverity.Info, cmd.Origin == SessionOrigin.Makeup ? "Thêm buổi học bù" : "Thêm buổi học thêm",
            cls.Code, $"{cmd.Date:dd/MM/yyyy} {TimeText.Of(cmd.StartTime)}");
        return new SessionResult(await reader.ReloadAsync(session.Id, ct), warnings);
    }

    /// <summary>Danh sách GV giống lớp = không ghi đè</summary>
    public static List<Guid> Normalize(List<Guid>? ids, IReadOnlyCollection<Guid> classIds)
    {
        var list = (ids ?? []).Distinct().ToList();
        return list.Count == 0 || (list.Count == classIds.Count && list.All(classIds.Contains)) ? [] : list;
    }
}
