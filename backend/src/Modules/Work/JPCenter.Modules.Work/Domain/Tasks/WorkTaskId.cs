using JPCenter.SharedKernel.Primitives;

namespace JPCenter.Modules.Work.Domain.Tasks;

/// <summary>Id công việc</summary>
public readonly record struct WorkTaskId(Guid Value)
{
    public static WorkTaskId New() => new(IdGenerator.NewId());
}
