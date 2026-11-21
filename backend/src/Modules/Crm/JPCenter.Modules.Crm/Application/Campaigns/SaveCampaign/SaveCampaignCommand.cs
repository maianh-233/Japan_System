using JPCenter.Application.Common.Auditing;
using JPCenter.Application.Common.Messaging;
using JPCenter.Application.Common.Security;
using JPCenter.Modules.Crm.Application.Abstractions;
using JPCenter.Modules.Crm.Application.Campaigns.Common;
using JPCenter.Modules.Crm.Application.Common;
using JPCenter.Modules.Crm.Domain.Campaigns;
using JPCenter.Modules.Crm.Domain.Leads;
using JPCenter.SharedKernel.Primitives;
using Microsoft.EntityFrameworkCore;

namespace JPCenter.Modules.Crm.Application.Campaigns.SaveCampaign;

/// <summary>Ô nhập chiến dịch (CRM-16 bước 1)</summary>
public sealed record CampaignInput(string? Code, string? Name, LeadSource Channel, List<Guid>? BranchIds, DateOnly StartDate, DateOnly EndDate,
    decimal Budget, decimal Spent, int? TargetLeads, Guid OwnerId, string? Utm, List<Guid>? CourseIds, string? Note);

/// <summary>
/// CRM-16 — tạo (<paramref name="Id"/> null) / sửa chiến dịch: ngày hợp lệ (E1), mã + UTM không trùng (E2), tiền VND hợp lệ,
/// chi nhánh trong phạm vi. Chống sửa đè bằng <paramref name="Version"/>.
/// </summary>
public sealed record SaveCampaignCommand(Guid? Id, CampaignInput Input, uint Version) : ICommand<CampaignDto>;

public sealed class SaveCampaignHandler(ICrmDbContext db, CrmAccess access, CampaignReader reader, IAuditTrail audit)
    : ICommandHandler<SaveCampaignCommand, CampaignDto>
{
    public async Task<Result<CampaignDto>> Handle(SaveCampaignCommand cmd, CancellationToken ct)
    {
        var i = cmd.Input;
        var action = cmd.Id is null ? PermissionAction.Create : PermissionAction.Update;
        if ((i.BranchIds ?? []).Any(b => !access.InBranch(CrmAccess.Campaigns, action, b))) return CrmAccess.BranchNotAllowed;
        var budget = Money.Create(i.Budget);
        if (budget.IsFailure) return budget.Error;
        var spent = Money.Create(i.Spent);
        if (spent.IsFailure) return spent.Error;
        var details = new CampaignDetails(i.Code ?? "", i.Name ?? "", i.Channel, (i.BranchIds ?? []).Select(b => new BranchId(b)).ToList(), i.StartDate,
            i.EndDate, budget.Value, spent.Value, i.TargetLeads, i.OwnerId, i.Utm, i.CourseIds ?? [], i.Note);

        Campaign campaign;
        if (cmd.Id is { } id)
        {
            var found = await db.Campaigns.FirstOrDefaultAsync(c => c.Id == new CampaignId(id), ct);
            if (found is null) return CampaignErrors.NotFound;
            if (cmd.Version != 0) ((DbContext)db).Entry(found).Property("xmin").OriginalValue = cmd.Version;
            if (found.Update(details) is { IsFailure: true } bad) return bad.Error;
            campaign = found;
        }
        else
        {
            var created = Campaign.Create(details);
            if (created.IsFailure) return created.Error;
            campaign = created.Value;
            db.Campaigns.Add(campaign);
        }

        if (await db.Campaigns.AnyAsync(c => c.Id != campaign.Id && c.Code == campaign.Code, ct)) return CampaignErrors.DuplicateCode;
        if (campaign.Utm is { } utm && await db.Campaigns.AnyAsync(c => c.Id != campaign.Id && c.Utm == utm, ct)) return CampaignErrors.DuplicateUtm;

        await db.SaveChangesAsync(ct);
        audit.Record(AuditCategory.Data, AuditSeverity.Info, cmd.Id is null ? "Tạo chiến dịch" : "Sửa chiến dịch", $"{campaign.Code} — {campaign.Name}");
        return await reader.ToDtoAsync(campaign, ct);
    }
}
