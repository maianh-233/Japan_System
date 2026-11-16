namespace JPCenter.SharedKernel.Domain;

/// <summary>Base cho mọi entity — so sánh theo Id.</summary>
public abstract class Entity<TId> where TId : struct
{
    public TId Id { get; protected init; }

    protected Entity(TId id) => Id = id;

    /// <summary>Cho EF Core.</summary>
    protected Entity() { }
}
