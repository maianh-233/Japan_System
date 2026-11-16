using JPCenter.SharedKernel.Primitives;

namespace JPCenter.Modules.System.Domain.OneTimeTokens;

/// <summary>Id mã dùng 1 lần</summary>
public readonly record struct OneTimeTokenId(Guid Value)
{
    public static OneTimeTokenId New() => new(IdGenerator.NewId());
}
