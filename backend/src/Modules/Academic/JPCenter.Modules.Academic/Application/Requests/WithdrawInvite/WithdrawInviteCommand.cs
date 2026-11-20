using JPCenter.Application.Common.Messaging;
using JPCenter.Application.Common.Realtime;
using JPCenter.Application.Common.Security;
using JPCenter.Modules.Academic.Application.Abstractions;
using JPCenter.Modules.Academic.Application.Requests.Common;
using JPCenter.Modules.Academic.Domain.Requests;
using JPCenter.SharedKernel.Domain;
using JPCenter.SharedKernel.Primitives;
using Microsoft.EntityFrameworkCore;

namespace JPCenter.Modules.Academic.Application.Requests.WithdrawInvite;

/// <summary>DT-16 — giáo vụ thu hồi lời mời đang chờ ("Giáo vụ thu hồi") → realtime <c>substitute-invite.closed</c> cho GV được mời</summary>
public sealed record WithdrawInviteCommand(Guid Id) : ICommand<InviteDto>;

public sealed class WithdrawInviteHandler(IAcademicDbContext db, RequestReader reader, IClock clock, IRealtimeNotifier realtime)
    : ICommandHandler<WithdrawInviteCommand, InviteDto>
{
    public const string Reason = "Giáo vụ thu hồi";

    public async Task<Result<InviteDto>> Handle(WithdrawInviteCommand cmd, CancellationToken ct)
    {
        var invite = await db.SubstituteInvites.FirstOrDefaultAsync(i => i.Id == new SubstituteInviteId(cmd.Id), ct);
        if (invite is null || !reader.InBranch(PermissionAction.Approve, invite.BranchId)) return RequestErrors.InviteNotFound;
        if (!invite.IsPending) return RequestErrors.InviteClosed;
        invite.Expire(Reason, clock.UtcNow);
        await db.SaveChangesAsync(ct);

        var dto = (await reader.InviteDtosAsync([invite], ct))[0];
        await realtime.ToEmployeesAsync([invite.ToTeacherId], "substitute-invite.closed", dto, ct);
        return dto;
    }
}
