using JPCenter.SharedKernel.Primitives;

namespace JPCenter.Modules.Academic.Domain.Exams;

public readonly record struct ExamAttemptId(Guid Value)
{
    public static ExamAttemptId New() => new(IdGenerator.NewId());
}
