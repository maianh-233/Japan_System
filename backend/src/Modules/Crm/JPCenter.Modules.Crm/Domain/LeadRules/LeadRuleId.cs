using JPCenter.SharedKernel.Primitives;

namespace JPCenter.Modules.Crm.Domain.LeadRules;

/// <summary>Id quy tắc chia lead</summary>
public readonly record struct LeadRuleId(Guid Value)
{
    public static LeadRuleId New() => new(IdGenerator.NewId());
}
