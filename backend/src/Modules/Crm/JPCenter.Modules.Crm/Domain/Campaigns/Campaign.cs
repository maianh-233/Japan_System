using JPCenter.SharedKernel.Domain;
using JPCenter.SharedKernel.Primitives;

namespace JPCenter.Modules.Crm.Domain.Campaigns;

/// <summary>
/// Chiến dịch marketing mang lead về (CRM-16). Trạng thái: dự kiến → đang chạy ⇄ tạm dừng → kết thúc.
/// Số liệu (số lead, chốt, chi phí / lead) tính khi đọc — <see cref="CampaignStats"/>. Bảng: crm.campaigns
/// </summary>
public sealed class Campaign : AggregateRoot<CampaignId>, IAuditable, ISoftDeletable
{
    private Campaign() { }

    public string Code { get; private set; } = null!;
    public string Name { get; private set; } = null!;
    /// <summary>Kênh chạy — trùng nguồn lead</summary>
    public Leads.LeadSource Channel { get; private set; }
    public List<BranchId> BranchIds { get; private set; } = [];
    public DateOnly StartDate { get; private set; }
    public DateOnly EndDate { get; private set; }
    public Money Budget { get; private set; }
    public Money Spent { get; private set; }
    /// <summary>Chỉ tiêu số lead</summary>
    public int? TargetLeads { get; private set; }
    public CampaignStatus Status { get; private set; }
    /// <summary>Người phụ trách (Hr.Employee)</summary>
    public Guid OwnerId { get; private set; }
    /// <summary>Mã UTM (chữ thường) — lead từ form có mã này tự gắn chiến dịch, kể cả khi đã kết thúc (CRM-16.E4)</summary>
    public string? Utm { get; private set; }
    /// <summary>Khóa học quảng bá (Academic.Course)</summary>
    public List<Guid> CourseIds { get; private set; } = [];
    public string? Note { get; private set; }

    public static Result<Campaign> Create(CampaignDetails details)
    {
        var campaign = new Campaign { Id = CampaignId.New(), Status = CampaignStatus.Planned };
        var set = campaign.Update(details);
        return set.IsFailure ? set.Error : campaign;
    }

    public Result Update(CampaignDetails d)
    {
        if (string.IsNullOrWhiteSpace(d.Code)) return CampaignErrors.CodeRequired;
        if (string.IsNullOrWhiteSpace(d.Name)) return CampaignErrors.NameRequired;
        if (d.BranchIds.Count == 0) return CampaignErrors.BranchRequired;
        if (d.EndDate < d.StartDate) return CampaignErrors.InvalidDates;
        if (d.TargetLeads is <= 0) return CampaignErrors.InvalidTarget;

        (Code, Name, Channel) = (d.Code.Trim().ToUpperInvariant(), d.Name.Trim(), d.Channel);
        (BranchIds, StartDate, EndDate) = (d.BranchIds.Distinct().ToList(), d.StartDate, d.EndDate);
        (Budget, Spent, TargetLeads, OwnerId) = (d.Budget, d.Spent, d.TargetLeads, d.OwnerId);
        (Utm, CourseIds) = (NormalizeUtm(d.Utm), d.CourseIds.Distinct().ToList());
        Note = string.IsNullOrWhiteSpace(d.Note) ? null : d.Note.Trim();
        return Result.Success();
    }

    /// <summary>Dự kiến / tạm dừng → đang chạy</summary>
    public Result Start() => Move(CampaignStatus.Running, CampaignStatus.Planned, CampaignStatus.Paused);

    /// <summary>Đang chạy → tạm dừng</summary>
    public Result Pause() => Move(CampaignStatus.Paused, CampaignStatus.Running);

    /// <summary>Kết thúc (không mở lại)</summary>
    public Result End() => Move(CampaignStatus.Ended, CampaignStatus.Planned, CampaignStatus.Running, CampaignStatus.Paused);

    /// <summary>Chuẩn hóa mã UTM để so khớp: bỏ khoảng trắng, chữ thường; rỗng → null</summary>
    public static string? NormalizeUtm(string? utm) => string.IsNullOrWhiteSpace(utm) ? null : utm.Trim().ToLowerInvariant();

    private Result Move(CampaignStatus to, params CampaignStatus[] from)
    {
        if (!from.Contains(Status)) return CampaignErrors.InvalidStatus;
        Status = to;
        return Result.Success();
    }
}
