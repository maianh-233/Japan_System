namespace JPCenter.SharedKernel.Primitives;

/// <summary>Id chi nhánh — dùng ở mọi module (aggregate Branch nằm ở module System).</summary>
public readonly record struct BranchId(Guid Value)
{
    public static BranchId New() => new(IdGenerator.NewId());
}
