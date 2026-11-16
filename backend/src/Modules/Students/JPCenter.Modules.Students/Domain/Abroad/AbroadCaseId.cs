using JPCenter.SharedKernel.Primitives;

namespace JPCenter.Modules.Students.Domain.Abroad;

/// <summary>Id hồ sơ du học / XKLĐ</summary>
public readonly record struct AbroadCaseId(Guid Value)
{
    public static AbroadCaseId New() => new(IdGenerator.NewId());
}
