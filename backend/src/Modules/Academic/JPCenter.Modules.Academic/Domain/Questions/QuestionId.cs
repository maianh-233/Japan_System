using JPCenter.SharedKernel.Primitives;

namespace JPCenter.Modules.Academic.Domain.Questions;

/// <summary>Id câu hỏi</summary>
public readonly record struct QuestionId(Guid Value)
{
    public static QuestionId New() => new(IdGenerator.NewId());
}
