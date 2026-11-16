using JPCenter.SharedKernel.Primitives;

namespace JPCenter.Modules.Crm.Domain.LeadRules;

/// <summary>Id dòng nhật ký chia lead</summary>
public readonly record struct LeadAssignmentLogId(Guid Value)
{
    public static LeadAssignmentLogId New() => new(IdGenerator.NewId());
}
