using JPCenter.SharedKernel.Domain;
using JPCenter.SharedKernel.Primitives;
using JPCenter.Modules.Crm.Domain.Leads;

namespace JPCenter.Modules.Crm.Domain.LeadRules;

/// <summary>Nhật ký chia lead: vì sao chọn Sale này (chỉ thêm). Bảng: crm.lead_assignment_logs</summary>
public sealed class LeadAssignmentLog : Entity<LeadAssignmentLogId>
{
    private LeadAssignmentLog() { }

    public LeadId LeadId { get; private set; }
    public Guid? SaleId { get; private set; }
    public LeadRuleId? RuleId { get; private set; }
    public DateTimeOffset At { get; private set; }
    public string Reason { get; private set; } = null!;

    public static LeadAssignmentLog Create(LeadId leadId, Guid? saleId, LeadRuleId? ruleId, string reason, DateTimeOffset at) =>
        new() { Id = LeadAssignmentLogId.New(), LeadId = leadId, SaleId = saleId, RuleId = ruleId, Reason = reason, At = at };
}
