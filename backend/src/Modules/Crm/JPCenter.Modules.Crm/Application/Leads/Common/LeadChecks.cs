using JPCenter.Modules.Crm.Application.Abstractions;
using JPCenter.Modules.Crm.Application.Common;
using JPCenter.Modules.Crm.Domain.Campaigns;
using JPCenter.Modules.Crm.Domain.Leads;
using JPCenter.Modules.System.Contracts;
using JPCenter.SharedKernel.Domain;
using JPCenter.SharedKernel.Primitives;
using Microsoft.EntityFrameworkCore;

namespace JPCenter.Modules.Crm.Application.Leads.Common;

/// <summary>Kiểm tra lead cần dữ liệu ngoài aggregate: chi nhánh đang hoạt động (CN-08), chiến dịch đúng kênh, Sale hợp lệ (CRM-02.E7), hẹn gọi lại.</summary>
public sealed class LeadChecks(ICrmDbContext db, IBranchDirectory branches, SalesTeam team, IClock clock)
{
    public static readonly Error BranchInactive = Error.Conflict("system.branch.inactive", "Chi nhánh đã ngừng hoạt động — chọn chi nhánh khác");
    public static readonly Error CampaignMismatch =
        Error.Validation("crm.lead.campaign_mismatch", "Chiến dịch không tồn tại hoặc không cùng kênh với nguồn lead");
    public static readonly Error FollowUpInPast = Error.Validation("crm.lead.invalid_follow_up", "Hẹn gọi lại không được trước hôm nay");

    /// <param name="newBranch">true khi tạo lead hoặc đổi chi nhánh — chỉ nhận chi nhánh đang hoạt động</param>
    public async Task<Result> CheckAsync(LeadInput input, bool newBranch, Guid? assigneeChanged, CancellationToken ct)
    {
        if (newBranch && await branches.FindAsync(input.BranchId, ct) is not { IsActive: true }) return BranchInactive;
        if (input.CampaignId is { } cid)
        {
            var campaign = await db.Campaigns.AsNoTracking().FirstOrDefaultAsync(c => c.Id == new CampaignId(cid), ct);
            if (campaign is null || campaign.Channel != input.Source) return CampaignMismatch;
        }
        if (input.NextFollowUpAt is { } follow && DateOnly.FromDateTime(follow.ToOffset(TimeSpan.FromHours(7)).DateTime) < clock.Today)
            return FollowUpInPast;
        if (assigneeChanged is { } sale && !await team.CanTakeAsync(sale, input.BranchId, ct)) return LeadErrors.InvalidAssignee;
        return Result.Success();
    }
}
