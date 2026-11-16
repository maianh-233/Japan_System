using JPCenter.SharedKernel.Domain;
using JPCenter.SharedKernel.Primitives;

namespace JPCenter.Modules.Crm.Domain.Campaigns;

/// <summary>Chiến dịch marketing mang lead về. Bảng: crm.campaigns</summary>
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
    /// <summary>Mã UTM — lead từ form có mã này tự gắn chiến dịch</summary>
    public string? Utm { get; private set; }
    /// <summary>Khóa học quảng bá (Academic.Course)</summary>
    public List<Guid> CourseIds { get; private set; } = [];
    public string? Note { get; private set; }

    public static Result<Campaign> Create(string code, string name, Leads.LeadSource channel, List<BranchId> branchIds,
        DateOnly start, DateOnly end, Money budget, Guid ownerId, string? utm, List<Guid> courseIds, int? targetLeads)
    {
        if (end < start) return Error.Validation("crm.campaign.invalid_dates", "Ngày kết thúc phải sau ngày bắt đầu");
        return new Campaign
        {
            Id = CampaignId.New(), Code = code, Name = name, Channel = channel, BranchIds = branchIds, StartDate = start, EndDate = end,
            Budget = budget, Spent = Money.Zero, OwnerId = ownerId, Utm = utm, CourseIds = courseIds, TargetLeads = targetLeads,
            Status = CampaignStatus.Planned,
        };
    }

    public void RecordSpend(Money amount) => Spent += amount;
    public void Start() => Status = CampaignStatus.Running;
    public void Pause() => Status = CampaignStatus.Paused;
    public void End() => Status = CampaignStatus.Ended;
}
