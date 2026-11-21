using JPCenter.SharedKernel.Domain;
using JPCenter.SharedKernel.Primitives;
using JPCenter.Modules.Crm.Domain.Leads;

namespace JPCenter.Modules.Crm.Domain.LeadRules;

/// <summary>
/// Quy tắc chia lead + SLA. Lead mới chưa có Sale → duyệt quy tắc đang bật theo Priority, quy tắc ĐẦU TIÊN khớp chọn Sale
/// (<see cref="LeadRouter"/>). Bảng: crm.lead_rules. Xóa mềm — nhật ký chia lead cũ vẫn giữ (CRM-09.E4).
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
    /// <summary>Nhóm Sale nhận lead (Hr.Employee) — thứ tự = thứ tự xoay vòng</summary>
    public List<Guid> SaleIds { get; private set; } = [];
    /// <summary>Chỉ chia cho Sale đang trong ca (theo Xếp ca)</summary>
    public bool OnlyOnShift { get; private set; }
    /// <summary>Thời hạn liên hệ lần đầu (phút)</summary>
    public int SlaMinutes { get; private set; }
    /// <summary>Quá SLA → báo cho những người này (Hr.Employee); rỗng = Quản lý chi nhánh</summary>
    public List<Guid> EscalateToIds { get; private set; } = [];
    /// <summary>Quá 2×SLA → tự chuyển Sale khác trong nhóm</summary>
    public bool ReassignOnBreach { get; private set; }
    /// <summary>Con trỏ xoay vòng (vị trí Sale được giao gần nhất; -1 = chưa giao ai)</summary>
    public int Cursor { get; private set; }

    public static Result<LeadRule> Create(LeadRuleSettings settings, int priority)
    {
        var rule = new LeadRule { Id = LeadRuleId.New(), Priority = priority, Active = true, Cursor = -1 };
        var set = rule.Update(settings);
        return set.IsFailure ? set.Error : rule;
    }

    /// <summary>CRM-09 — sửa quy tắc. Đổi nhóm Sale → con trỏ xoay vòng về đầu.</summary>
    public Result Update(LeadRuleSettings s)
    {
        if (string.IsNullOrWhiteSpace(s.Name)) return LeadRuleErrors.NameRequired;
        var sales = s.SaleIds.Distinct().ToList();
        if (sales.Count == 0) return LeadRuleErrors.NoSale;
        if (s.SlaMinutes <= 0 || s.SlaMinutes > LeadRuleErrors.MaxSlaMinutes) return LeadRuleErrors.InvalidSla;

        if (!sales.SequenceEqual(SaleIds)) Cursor = -1;
        Name = s.Name.Trim();
        (Sources, BranchIds) = (s.Sources.Distinct().ToList(), s.BranchIds.Distinct().ToList());
        Interests = s.Interests.Where(i => !string.IsNullOrWhiteSpace(i)).Select(i => i.Trim()).Distinct().ToList();
        (Method, SaleIds, OnlyOnShift, SlaMinutes) = (s.Method, sales, s.OnlyOnShift, s.SlaMinutes);
        (EscalateToIds, ReassignOnBreach) = (s.EscalateToIds.Distinct().ToList(), s.ReassignOnBreach);
        return Result.Success();
    }

    /// <summary>Khớp điều kiện nguồn ∧ chi nhánh ∧ khóa quan tâm (rỗng = mọi giá trị). Không xét bật / tắt.</summary>
    public bool Matches(LeadSource source, BranchId branchId, string? interest) =>
        (Sources.Count == 0 || Sources.Contains(source))
        && (BranchIds.Count == 0 || BranchIds.Contains(branchId))
        && (Interests.Count == 0 || (interest is not null && Interests.Contains(interest.Trim())));

    /// <summary>Lưu vị trí Sale vừa được giao (xoay vòng)</summary>
    public void MoveCursor(int index) => Cursor = index;

    /// <summary>CRM-09.E5 — đổi thứ tự (Application gửi cả danh sách)</summary>
    public void SetPriority(int priority) => Priority = priority;

    public void Enable() => Active = true;
    public void Disable() => Active = false;
}
