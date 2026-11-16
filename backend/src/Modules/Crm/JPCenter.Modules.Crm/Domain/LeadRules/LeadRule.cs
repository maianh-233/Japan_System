using JPCenter.SharedKernel.Domain;
using JPCenter.SharedKernel.Primitives;
using JPCenter.Modules.Crm.Domain.Leads;

namespace JPCenter.Modules.Crm.Domain.LeadRules;

/// <summary>
/// Quy tắc chia lead + SLA. Lead mới chưa có Sale → duyệt quy tắc đang bật theo Priority, quy tắc ĐẦU TIÊN khớp chọn Sale.
/// Bảng: crm.lead_rules
/// </summary>
public sealed class LeadRule : AggregateRoot<LeadRuleId>, IAuditable, ISoftDeletable
{
    private LeadRule() { }

    public string Name { get; private set; } = null!;
    /// <summary>Thứ tự ưu tiên (nhỏ chạy trước)</summary>
    public int Priority { get; private set; }
    public bool Active { get; private set; }
    /// <summary>Điều kiện — rỗng = mọi giá trị</summary>
    public List<LeadSource> Sources { get; private set; } = [];
    public List<BranchId> BranchIds { get; private set; } = [];
    public List<string> Interests { get; private set; } = [];
    public RouteMethod Method { get; private set; }
    /// <summary>Nhóm Sale nhận lead (Hr.Employee)</summary>
    public List<Guid> SaleIds { get; private set; } = [];
    /// <summary>Chỉ chia cho Sale đang trong ca (theo Xếp ca)</summary>
    public bool OnlyOnShift { get; private set; }
    /// <summary>Thời hạn liên hệ lần đầu (phút)</summary>
    public int SlaMinutes { get; private set; }
    /// <summary>Quá SLA → báo cho những người này (Hr.Employee)</summary>
    public List<Guid> EscalateToIds { get; private set; } = [];
    /// <summary>Quá 2×SLA → tự chuyển Sale khác trong nhóm</summary>
    public bool ReassignOnBreach { get; private set; }
    /// <summary>Con trỏ xoay vòng (vị trí Sale được giao gần nhất)</summary>
    public int Cursor { get; private set; }

    public static Result<LeadRule> Create(string name, int priority, List<LeadSource> sources, List<BranchId> branchIds,
        List<string> interests, RouteMethod method, List<Guid> saleIds, bool onlyOnShift, int slaMinutes,
        List<Guid> escalateToIds, bool reassignOnBreach)
    {
        if (saleIds.Count == 0) return Error.Validation("crm.lead_rule.no_sale", "Quy tắc phải có ít nhất 1 Sale");
        if (slaMinutes <= 0) return Error.Validation("crm.lead_rule.invalid_sla", "SLA phải lớn hơn 0 phút");
        return new LeadRule
        {
            Id = LeadRuleId.New(), Name = name, Priority = priority, Active = true, Sources = sources, BranchIds = branchIds,
            Interests = interests, Method = method, SaleIds = saleIds, OnlyOnShift = onlyOnShift, SlaMinutes = slaMinutes,
            EscalateToIds = escalateToIds, ReassignOnBreach = reassignOnBreach,
        };
    }

    public bool Matches(LeadSource source, BranchId branchId, string? interest) =>
        Active
        && (Sources.Count == 0 || Sources.Contains(source))
        && (BranchIds.Count == 0 || BranchIds.Contains(branchId))
        && (Interests.Count == 0 || (interest is not null && Interests.Contains(interest)));

    /// <summary>Lưu vị trí Sale vừa được giao (xoay vòng)</summary>
    public void MoveCursor(int index) => Cursor = index;

    public void Enable() => Active = true;
    public void Disable() => Active = false;
}
