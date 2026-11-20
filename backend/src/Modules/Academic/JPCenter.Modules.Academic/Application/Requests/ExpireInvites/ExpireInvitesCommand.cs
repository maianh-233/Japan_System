using JPCenter.Application.Common.Messaging;
using JPCenter.Application.Common.Realtime;
using JPCenter.Modules.Academic.Application.Abstractions;
using JPCenter.Modules.Academic.Application.Requests.Common;
using JPCenter.Modules.Academic.Domain.Requests;
using JPCenter.SharedKernel.Domain;
using JPCenter.SharedKernel.Primitives;
using Microsoft.EntityFrameworkCore;

namespace JPCenter.Modules.Academic.Application.Requests.ExpireInvites;

/// <summary>
/// DT-16 bước 3 — job mỗi 5 phút: lời mời quá hạn trả lời mà chưa ai nhận → hết hiệu lực "Quá hạn trả lời";
/// báo realtime <c>substitute-invite.expired</c> cho giáo vụ đã mời (chọn cách khác) và GV được mời. Trả số lời mời đã hết hạn.
/// </summary>
public sealed record ExpireInvitesCommand : ICommand<int>;

public sealed class ExpireInvitesHandler(IAcademicDbContext db, RequestReader reader, IClock clock, IRealtimeNotifier realtime)
    : ICommandHandler<ExpireInvitesCommand, int>
{
    public async Task<Result<int>> Handle(ExpireInvitesCommand cmd, CancellationToken ct)
    {
        var now = clock.UtcNow;
        var overdue = await db.SubstituteInvites.IgnoreQueryFilters()
            .Where(i => i.Status == InviteStatus.Pending && i.RespondBy < now).ToListAsync(ct);
        var expired = overdue.Where(i => i.ExpireIfOverdue(now)).ToList();
        if (expired.Count == 0) return 0;
        await db.SaveChangesAsync(ct);

        foreach (var dto in await reader.InviteDtosAsync(expired, ct))
            await realtime.ToEmployeesAsync([dto.InvitedById, dto.ToTeacherId], "substitute-invite.expired", dto, ct);
        return expired.Count;
    }
}
