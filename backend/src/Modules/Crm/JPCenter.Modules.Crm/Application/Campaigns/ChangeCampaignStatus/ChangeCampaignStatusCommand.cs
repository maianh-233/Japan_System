using JPCenter.Application.Common.Messaging;
using JPCenter.Application.Common.Security;
using JPCenter.Modules.Crm.Application.Abstractions;
using JPCenter.Modules.Crm.Application.Campaigns.Common;
using JPCenter.Modules.Crm.Domain.Campaigns;
using JPCenter.SharedKernel.Primitives;
using Microsoft.EntityFrameworkCore;

namespace JPCenter.Modules.Crm.Application.Campaigns.ChangeCampaignStatus;

/// <summary>CRM-16 bước 2 — dự kiến → đang chạy ⇄ tạm dừng → kết thúc (kết thúc không mở lại)</summary>
public sealed record ChangeCampaignStatusCommand(Guid Id, CampaignStatus Status) : ICommand<CampaignDto>;

public sealed class ChangeCampaignStatusHandler(ICrmDbContext db, CampaignReader reader) : ICommandHandler<ChangeCampaignStatusCommand, CampaignDto>
{
    public async Task<Result<CampaignDto>> Handle(ChangeCampaignStatusCommand cmd, CancellationToken ct)
    {
        var campaign = (await reader.VisibleAsync(db.Campaigns.Where(c => c.Id == new CampaignId(cmd.Id)), PermissionAction.Update, ct)).FirstOrDefault();
        if (campaign is null) return CampaignErrors.NotFound;
        var moved = cmd.Status switch
        {
            CampaignStatus.Running => campaign.Start(),
            CampaignStatus.Paused => campaign.Pause(),
            CampaignStatus.Ended => campaign.End(),
            _ => CampaignErrors.InvalidStatus,
        };
        if (moved.IsFailure) return moved.Error;
        await db.SaveChangesAsync(ct);
        return await reader.ToDtoAsync(campaign, ct);
    }
}
