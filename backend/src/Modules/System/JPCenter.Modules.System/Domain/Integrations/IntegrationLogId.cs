using JPCenter.SharedKernel.Primitives;

namespace JPCenter.Modules.System.Domain.Integrations;

/// <summary>Id dòng nhật ký tích hợp</summary>
public readonly record struct IntegrationLogId(Guid Value)
{
    public static IntegrationLogId New() => new(IdGenerator.NewId());
}
