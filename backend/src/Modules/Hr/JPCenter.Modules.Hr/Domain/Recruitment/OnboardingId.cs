using JPCenter.SharedKernel.Primitives;

namespace JPCenter.Modules.Hr.Domain.Recruitment;

/// <summary>Id hồ sơ nhận việc</summary>
public readonly record struct OnboardingId(Guid Value)
{
    public static OnboardingId New() => new(IdGenerator.NewId());
}
