namespace JPCenter.SharedKernel.Primitives;

/// <summary>Sinh Guid v7 (tăng dần theo thời gian — index B-tree tốt hơn Guid v4).</summary>
public static class IdGenerator
{
    public static Guid NewId() => Guid.CreateVersion7();
}
