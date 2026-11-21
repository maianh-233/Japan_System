using JPCenter.Modules.Crm.Domain.LeadRules;
using JPCenter.Modules.Crm.Domain.Leads;

namespace JPCenter.Modules.Crm.Application.LeadRules.Common;

/// <summary>Quy tắc chia lead — khớp <c>LeadRule</c> của frontend (người nhận cảnh báo theo Id nhân viên) + cảnh báo Sale đã nghỉ (CRM-09.E3)</summary>
public sealed record LeadRuleDto(
    Guid Id,
    string Name,
    int Priority,
    bool Active,
    IReadOnlyList<LeadSource> Sources,
    IReadOnlyList<Guid> BranchIds,
    IReadOnlyList<string> Interests,
    RouteMethod Method,
    IReadOnlyList<Guid> SaleIds,
    bool OnlyOnShift,
    int SlaMinutes,
    IReadOnlyList<Guid> EscalateToIds,
    bool ReassignOnBreach,
    int Cursor,
    IReadOnlyList<string> Warnings,
    uint Version);

/// <summary>Dòng nhật ký chia lead — khớp <c>AssignmentLog</c> + tên để hiển thị</summary>
public sealed record AssignmentLogDto(Guid Id, Guid LeadId, string LeadCode, string LeadName, Guid? SaleId, string? SaleName, Guid? RuleId,
    string? RuleName, DateTimeOffset At, string Reason);
