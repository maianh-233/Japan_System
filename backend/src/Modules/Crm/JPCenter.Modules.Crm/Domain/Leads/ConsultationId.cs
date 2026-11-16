using JPCenter.SharedKernel.Primitives;

namespace JPCenter.Modules.Crm.Domain.Leads;

/// <summary>Id 1 lần tư vấn</summary>
public readonly record struct ConsultationId(Guid Value)
{
    public static ConsultationId New() => new(IdGenerator.NewId());
}
