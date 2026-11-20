using FluentValidation;
using JPCenter.Application.Common.Auditing;
using JPCenter.Application.Common.IntegrationEvents;
using JPCenter.Application.Common.Messaging;
using JPCenter.Application.Common.Realtime;
using JPCenter.Modules.Academic.Application.Abstractions;
using JPCenter.Modules.Academic.Application.ClassSessions.Common;
using JPCenter.Modules.Academic.Application.Common;
using JPCenter.Modules.Academic.Application.Requests.Common;
using JPCenter.Modules.Academic.Domain.Requests;
using JPCenter.SharedKernel.Domain;
using JPCenter.SharedKernel.Primitives;
using Microsoft.EntityFrameworkCore;
using Ev = JPCenter.Modules.Academic.Contracts.IntegrationEvents;

namespace JPCenter.Modules.Academic.Application.Requests.RespondInvite;

/// <summary>
/// DT-16 bước 2 — GV nhận / từ chối lời mời dạy thay trên cổng. Nhận: ai nhận TRƯỚC thì dạy — buổi ghi đè GV = người nhận,
/// lời mời khác của buổi hết hiệu lực "GV khác đã nhận". 2 GV bấm cùng lúc (E5): khóa lạc quan trên buổi → người sau <c>academic.invite.closed</c>.
/// Realtime: <c>substitute-invite.accepted</c> / <c>.declined</c> (giáo vụ mời + GV vắng), <c>.closed</c> (GV khác được mời).
/// </summary>
public sealed record RespondInviteCommand(Guid Id, bool Accept, string? Reason) : ICommand<InviteDto>;

public sealed class RespondInviteValidator : AbstractValidator<RespondInviteCommand>
{
    public RespondInviteValidator() => RuleFor(x => x.Reason).MaximumLength(500);
}

public sealed class RespondInviteHandler(IAcademicDbContext db, TeacherSessions teacher, RequestReader reader, PeopleNames people, IClock clock,
    IIntegrationEventPublisher events, IRealtimeNotifier realtime, IAuditTrail audit) : ICommandHandler<RespondInviteCommand, InviteDto>
{
    public const string TakenByOther = "GV khác đã nhận";

    public async Task<Result<InviteDto>> Handle(RespondInviteCommand cmd, CancellationToken ct)
    {
        var invite = await db.SubstituteInvites.FirstOrDefaultAsync(i => i.Id == new SubstituteInviteId(cmd.Id) && i.ToTeacherId == teacher.Me, ct);
        if (invite is null) return RequestErrors.InviteNotFound;
        if (!cmd.Accept) return await DeclineAsync(invite, cmd.Reason, ct);

        var session = await db.ClassSessions.FirstOrDefaultAsync(s => s.Id == invite.SessionId, ct);
        if (session is null)
        {
            invite.Expire(RequestErrors.SessionGone.Message, clock.UtcNow);
            await db.SaveChangesAsync(ct);
            return RequestErrors.SessionGone;
        }

        var accepted = invite.Accept(clock.UtcNow);
        if (accepted.IsFailure)
        {
            await db.SaveChangesAsync(ct); // quá hạn → lưu trạng thái hết hiệu lực
            return accepted.Error;
        }
        if (session.AssignSubstitute(teacher.Me, invite.Reason, clock.LocalNow) is { IsFailure: true } assigned) return assigned.Error;

        var others = await db.SubstituteInvites.Where(i => i.SessionId == invite.SessionId && i.Id != invite.Id && i.Status == InviteStatus.Pending)
            .ToListAsync(ct);
        foreach (var o in others) o.Expire(TakenByOther, clock.UtcNow);

        var names = await people.OfAsync([teacher.Me], ct);
        if (invite.CancelRequestId is { } rid
            && await db.SessionCancelRequests.FirstOrDefaultAsync(r => r.Id == rid, ct) is { } request)
            request.AddResultNote($"GV {names.GetValueOrDefault(teacher.Me, "—")} đã nhận dạy thay");

        events.Publish(new Ev.SubstituteAccepted(session.Id.Value, session.ClassId.Value, invite.FromTeacherId, teacher.Me));
        try
        {
            await db.SaveChangesAsync(ct);
        }
        catch (DbUpdateConcurrencyException)
        {
            return RequestErrors.InviteClosed; // GV khác vừa nhận cùng lúc (E5)
        }

        var dtos = await reader.InviteDtosAsync([invite, .. others], ct);
        var dto = dtos.First(d => d.Id == invite.Id.Value);
        audit.Record(AuditCategory.Data, AuditSeverity.Info, "Nhận dạy thay", $"{dto.ClassCode} · buổi {dto.Session?.No}", dto.ToTeacherName);
        await realtime.ToEmployeesAsync([invite.InvitedBy, invite.FromTeacherId], "substitute-invite.accepted", dto, ct);
        foreach (var o in dtos.Where(d => d.Id != invite.Id.Value))
            await realtime.ToEmployeesAsync([o.ToTeacherId], "substitute-invite.closed", o, ct);
        return dto;
    }

    private async Task<Result<InviteDto>> DeclineAsync(SubstituteInvite invite, string? reason, CancellationToken ct)
    {
        if (invite.Decline(reason, clock.UtcNow) is { IsFailure: true } r) return r.Error;
        await db.SaveChangesAsync(ct);
        var dto = (await reader.InviteDtosAsync([invite], ct))[0];
        await realtime.ToEmployeesAsync([invite.InvitedBy], "substitute-invite.declined", dto, ct);
        return dto;
    }
}
