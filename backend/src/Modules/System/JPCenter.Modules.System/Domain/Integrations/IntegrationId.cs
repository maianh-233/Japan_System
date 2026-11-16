using JPCenter.SharedKernel.Primitives;

namespace JPCenter.Modules.System.Domain.Integrations;

/// <summary>Id tích hợp</summary>
public readonly record struct IntegrationId(Guid Value)
{
    public static IntegrationId New() => new(IdGenerator.NewId());
}
