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
namespace JPCenter.Modules.Academic.Application.ClassSessions.EditSession;

/// <summary>
/// DT-10 — dời ngày giờ / đổi GV (dạy thay) / trợ giảng / phòng cho riêng 1 buổi chưa diễn ra. Lý do bắt buộc. GV / phòng giống lớp = bỏ ghi đè.
/// Trùng lịch chỉ cảnh báo. Sửa đè → 409.
/// </summary>
public sealed record EditSessionCommand(Guid Id, DateOnly Date, TimeOnly StartTime, TimeOnly EndTime, List<Guid>? TeacherIds,
    List<Guid>? AssistantIds, Guid? RoomId, string Reason, uint RowVersion) : ICommand<SessionResult>;

public sealed class EditSessionValidator : AbstractValidator<EditSessionCommand>
{
    public EditSessionValidator() => RuleFor(x => x.Reason).NotEmpty().WithMessage("Nhập lý do").MaximumLength(500);
}

public sealed class EditSessionHandler(IAcademicDbContext db, SessionReader reader, ClassRules rules, SessionConflicts conflicts,
    IIntegrationEventPublisher events, IAuditTrail audit) : ICommandHandler<EditSessionCommand, SessionResult>
{
    public async Task<Result<SessionResult>> Handle(EditSessionCommand cmd, CancellationToken ct)
    {
        if (await reader.ForUpdateAsync(cmd.Id, PermissionAction.Update, ct) is not var (session, cls)) return SessionErrors.NotFound;
        ((DbContext)db).Entry(session).Property("xmin").OriginalValue = cmd.RowVersion;

        var teachers = CreateSession.CreateSessionHandler.Normalize(cmd.TeacherIds, cls.TeacherIds);
        var assistants = cmd.AssistantIds is null ? null
            : cmd.AssistantIds.Distinct().Order().SequenceEqual(cls.AssistantIds.Order()) ? null : cmd.AssistantIds.Distinct().ToList();
        var existing = session.TeacherIds.Concat(session.AssistantIds ?? []).ToList();
        var newcomers = teachers.Concat(assistants ?? []).Where(i => !existing.Contains(i)).ToList();
        if (await rules.EnsureStaffAsync(newcomers, cls, ct) is { IsFailure: true } staff) return staff.Error;
        var room = cls.Online || cmd.RoomId is not { } rid || rid == cls.RoomId?.Value ? (RoomId?)null : new RoomId(rid);
        if (room is { } r && r != session.RoomId && await rules.RoomCapacityAsync(r.Value, cls.BranchId.Value, false, null, ct) is { IsFailure: true } bad)
            return bad.Error;

        if (session.Edit(cmd.Date, cmd.StartTime, cmd.EndTime, teachers, assistants, room, cmd.Reason) is { IsFailure: true } e) return e.Error;
        var warnings = await conflicts.WarningsAsync(new SessionProbe(session.Date, session.StartTime, session.EndTime,
            SessionConflicts.PeopleOf(session, cls), cls.Online ? null : (session.RoomId ?? cls.RoomId)?.Value, session.Id), ct);
        events.Publish(new Ev.SessionRescheduled(session.Id.Value, cls.Id.Value, session.Date, session.StartTime, session.EndTime,
            SessionReader.Teachers(session, cls).ToList(), session.RescheduleReason!));
        await db.SaveChangesAsync(ct);
        audit.Record(AuditCategory.Data, AuditSeverity.Info, "Sửa buổi học", $"{cls.Code} · buổi {session.No}", session.RescheduleReason);
        return new SessionResult(await reader.ReloadAsync(session.Id, ct), warnings);
    }
}
