using JPCenter.Application.Common.Messaging;
using JPCenter.Application.Common.Security;
using JPCenter.Modules.Crm.Application.Abstractions;
using JPCenter.Modules.Crm.Application.Campaigns.Common;
using JPCenter.SharedKernel.Primitives;
using Microsoft.EntityFrameworkCore;

namespace JPCenter.Modules.Crm.Application.Campaigns.GetCampaigns;

/// <summary>CRM-16 — danh sách chiến dịch kèm số liệu, mới bắt đầu trước</summary>
public sealed record GetCampaignsQuery : IQuery<IReadOnlyList<CampaignDto>>;

public sealed class GetCampaignsHandler(ICrmDbContext db, CampaignReader reader) : IQueryHandler<GetCampaignsQuery, IReadOnlyList<CampaignDto>>
{
    public async Task<Result<IReadOnlyList<CampaignDto>>> Handle(GetCampaignsQuery query, CancellationToken ct)
    {
        var list = await reader.VisibleAsync(db.Campaigns.AsNoTracking().OrderByDescending(c => c.StartDate), PermissionAction.View, ct);
        return Result.Success(await reader.ToDtosAsync(list, ct));
    }
}
