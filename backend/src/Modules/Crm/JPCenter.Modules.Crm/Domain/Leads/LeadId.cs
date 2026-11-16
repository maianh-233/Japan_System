using JPCenter.SharedKernel.Primitives;

namespace JPCenter.Modules.Crm.Domain.Leads;

/// <summary>Id lead (người liên hệ — người học hoặc phụ huynh)</summary>
public readonly record struct LeadId(Guid Value)
{
    public static LeadId New() => new(IdGenerator.NewId());
}
