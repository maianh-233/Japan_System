using JPCenter.Modules.Crm.Domain.Leads;
using JPCenter.SharedKernel.Primitives;

namespace JPCenter.Modules.Crm.Domain.LeadRules;

/// <summary>Nội dung 1 quy tắc chia lead (CRM-09 thêm / sửa) — kiểm tra trong <see cref="LeadRule"/>.</summary>
public sealed record LeadRuleSettings(
    string Name,
    IReadOnlyList<LeadSource> Sources,
    IReadOnlyList<BranchId> BranchIds,
    IReadOnlyList<string> Interests,
    RouteMethod Method,
    IReadOnlyList<Guid> SaleIds,
    bool OnlyOnShift,
    int SlaMinutes,
    IReadOnlyList<Guid> EscalateToIds,
    bool ReassignOnBreach);
