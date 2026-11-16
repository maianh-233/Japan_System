using JPCenter.SharedKernel.Primitives;

namespace JPCenter.Modules.Academic.Domain.Curricula;

/// <summary>Id mốc thi trong lộ trình</summary>
public readonly record struct ExamMilestoneId(Guid Value)
{
    public static ExamMilestoneId New() => new(IdGenerator.NewId());
}
