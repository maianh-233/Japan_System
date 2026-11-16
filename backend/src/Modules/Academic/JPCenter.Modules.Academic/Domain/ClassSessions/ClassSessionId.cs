using JPCenter.SharedKernel.Primitives;

namespace JPCenter.Modules.Academic.Domain.ClassSessions;

/// <summary>Id buổi học</summary>
public readonly record struct ClassSessionId(Guid Value)
{
    public static ClassSessionId New() => new(IdGenerator.NewId());
}
