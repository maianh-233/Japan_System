using JPCenter.SharedKernel.Primitives;

namespace JPCenter.Modules.Students.Domain.CareCases;

/// <summary>Id lần liên hệ chăm sóc</summary>
public readonly record struct CareLogId(Guid Value)
{
    public static CareLogId New() => new(IdGenerator.NewId());
}
