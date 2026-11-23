using JPCenter.Application.Common.Messaging;
using JPCenter.Modules.Work.Application.Abstractions;
using JPCenter.Modules.Work.Application.Common;
using JPCenter.Modules.Work.Application.Handovers.Common;
using JPCenter.Modules.Work.Domain.Handovers;
using JPCenter.SharedKernel.Primitives;
using Microsoft.EntityFrameworkCore;

namespace JPCenter.Modules.Work.Application.Handovers.GetHandovers;

/// <summary>BG-03 — "Chờ tôi nhận" ở đầu + danh sách (mới nhất trước)</summary>
public sealed record HandoverListDto(IReadOnlyList<HandoverDto> PendingForMe, IReadOnlyList<HandoverDto> Items);

/// <summary>
/// BG-03 — bàn giao của chi nhánh trong phạm vi <c>work.handover.view</c>; không có quyền xem chi nhánh vẫn thấy bàn giao mình gửi / nhận.
/// </summary>
public sealed record GetHandoversQuery(HandoverStatus? Status, Guid? BranchId, int Days = 60) : IQuery<HandoverListDto>;

public sealed class GetHandoversHandler(IWorkDbContext db, HandoverDesk desk, WorkPeople people, JPCenter.SharedKernel.Domain.IClock clock)
    : IQueryHandler<GetHandoversQuery, HandoverListDto>
{
    public async Task<Result<HandoverListDto>> Handle(GetHandoversQuery q, CancellationToken ct)
    {
        var since = clock.Today.AddDays(-Math.Clamp(q.Days, 1, 366));
        var rows = (await db.Handovers.AsNoTracking().Where(h => h.Date >= since || h.Status == HandoverStatus.Pending).ToListAsync(ct))
            .Where(desk.CanSee)
            .Where(h => q.BranchId is not { } b || h.BranchId.Value == b)
            .OrderByDescending(h => h.HandedAt)
            .ToList();
        var me = people.MeId;
        var pending = rows.Where(h => h.Status == HandoverStatus.Pending && h.ToId == me).ToList();
        var items = rows.Where(h => q.Status is not { } s || h.Status == s).ToList();
        return new HandoverListDto(await desk.ToDtosAsync(pending, ct), await desk.ToDtosAsync(items, ct));
    }
}
