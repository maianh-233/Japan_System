using JPCenter.SharedKernel.Primitives;

namespace JPCenter.Modules.Academic.Domain.Assignments;

/// <summary>Id bài nộp</summary>
public readonly record struct SubmissionId(Guid Value)
{
    public static SubmissionId New() => new(IdGenerator.NewId());
}
