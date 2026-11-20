using FluentValidation;
using JPCenter.Application.Common.Auditing;
using JPCenter.Application.Common.Messaging;
using JPCenter.Application.Common.Realtime;
using JPCenter.Application.Common.Security;
using JPCenter.Modules.Academic.Application.Abstractions;
using JPCenter.Modules.Academic.Application.ClassSessions.Common;
using JPCenter.Modules.Academic.Application.Common;
using JPCenter.Modules.Academic.Application.Requests.Common;
using JPCenter.Modules.Academic.Domain.ClassSessions;
using JPCenter.Modules.Academic.Domain.Requests;
using JPCenter.SharedKernel.Domain;
using JPCenter.SharedKernel.Primitives;
using Microsoft.EntityFrameworkCore;

namespace JPCenter.Modules.Academic.Application.Requests.InviteSubstitutes;

/// <summary>
/// DT-16 bước 2 — giáo vụ mời ≥ 1 GV dạy thay 1 buổi chưa bắt đầu, có hạn trả lời. Mỗi GV 1 lời mời (GV đã có lời mời đang chờ → bỏ qua).
/// Gắn đơn hủy buổi (nếu có): đơn còn chờ → tự duyệt kèm ghi chú "Đã mời GV … dạy thay". Đẩy realtime <c>substitute-invite.created</c> cho GV được mời.
/// </summary>
public sealed record InviteSubstitutesCommand(Guid SessionId, Guid? CancelRequestId, IReadOnlyList<Guid> TeacherIds, DateTimeOffset RespondBy,
    string Reason) : ICommand<IReadOnlyList<InviteDto>>;

public sealed class InviteSubstitutesValidator : AbstractValidator<InviteSubstitutesCommand>
{
    public InviteSubstitutesValidator()
    {
        RuleFor(x => x.SessionId).NotEmpty();
        RuleFor(x => x.TeacherIds).NotEmpty().WithMessage("Chọn ít nhất 1 GV để mời dạy thay").Must(t => t.Count <= 20);
        RuleFor(x => x.Reason).NotEmpty().WithMessage("Nhập lý do cần dạy thay").MaximumLength(500);
    }
}

public sealed class InviteSubstitutesHandler(IAcademicDbContext db, RequestReader reader, AcademicAccess access, PeopleNames people, IClock clock,
    IRealtimeNotifier realtime, IAuditTrail audit) : ICommandHandler<InviteSubstitutesCommand, IReadOnlyList<InviteDto>>
{
    public async Task<Result<IReadOnlyList<InviteDto>>> Handle(InviteSubstitutesCommand cmd, CancellationToken ct)
    {
        var session = await db.ClassSessions.AsNoTracking().FirstOrDefaultAsync(s => s.Id == new ClassSessionId(cmd.SessionId), ct);
        if (session is null || !reader.InBranch(PermissionAction.Approve, session.BranchId)) return SessionErrors.NotFound;
        if (session.Status == SessionStatus.Cancelled) return SessionErrors.Cancelled;
        if (session.Status != SessionStatus.Scheduled || session.IsConfirmed) return SessionErrors.NotScheduled;
        if (clock.LocalNow >= session.StartsAt) return SessionErrors.AlreadyStarted;
        var cls = await db.Classes.IgnoreQueryFilters().AsNoTracking().FirstAsync(c => c.Id == session.ClassId, ct);

        SessionCancelRequest? request = null;
        if (cmd.CancelRequestId is { } rid)
        {
            request = await db.SessionCancelRequests.FirstOrDefaultAsync(r => r.Id == new SessionCancelRequestId(rid) && r.SessionId == session.Id, ct);
            if (request is null) return RequestErrors.CancelRequestNotFound;
        }
        var from = request?.TeacherId ?? SessionReader.Teachers(session, cls).FirstOrDefault();

        var waiting = await db.SubstituteInvites.Where(i => i.SessionId == session.Id && i.Status == InviteStatus.Pending)
            .Select(i => i.ToTeacherId).ToListAsync(ct);
        var created = new List<SubstituteInvite>();
        foreach (var to in cmd.TeacherIds.Distinct().Where(t => !waiting.Contains(t)))
        {
            var invite = SubstituteInvite.Create(session.Id, cls.Id, session.BranchId, request?.Id, from, to, cmd.Reason.Trim(), access.ActorId,
                clock.UtcNow, cmd.RespondBy);
            if (invite.IsFailure) return invite.Error;
            created.Add(invite.Value);
        }
        if (created.Count == 0) return RequestErrors.InviteTeachersRequired;
        db.SubstituteInvites.AddRange(created);

        if (request is not null)
        {
            var names = await people.OfAsync(created.Select(i => (Guid?)i.ToTeacherId), ct);
            var note = $"Đã mời GV {string.Join(", ", created.Select(i => names.GetValueOrDefault(i.ToTeacherId, "—")))} dạy thay";
            if (request.IsPending) request.Approve(access.ActorId, clock.UtcNow, note);
            else request.AddResultNote(note);
        }
        await db.SaveChangesAsync(ct);

        var dtos = await reader.InviteDtosAsync(created, ct);
        audit.Record(AuditCategory.Data, AuditSeverity.Info, "Mời dạy thay", $"{cls.Code} · buổi {session.No}",
            string.Join(", ", dtos.Select(d => d.ToTeacherName)));
        foreach (var dto in dtos) await realtime.ToEmployeesAsync([dto.ToTeacherId], "substitute-invite.created", dto, ct);
        return Result.Success(dtos);
    }
}
