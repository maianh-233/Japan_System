using JPCenter.SharedKernel.Primitives;

namespace JPCenter.Modules.Academic.Domain.Exams;

/// <summary>Id kết quả thi</summary>
public readonly record struct ExamResultId(Guid Value)
{
    public static ExamResultId New() => new(IdGenerator.NewId());
}
