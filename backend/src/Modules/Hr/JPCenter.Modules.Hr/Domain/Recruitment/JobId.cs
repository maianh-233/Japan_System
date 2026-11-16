using JPCenter.SharedKernel.Primitives;

namespace JPCenter.Modules.Hr.Domain.Recruitment;

/// <summary>Id tin tuyển dụng</summary>
public readonly record struct JobId(Guid Value)
{
    public static JobId New() => new(IdGenerator.NewId());
}
