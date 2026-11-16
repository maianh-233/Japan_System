using JPCenter.SharedKernel.Primitives;

namespace JPCenter.Modules.Academic.Domain.Flashcards;

/// <summary>Id tiến độ ôn 1 thẻ</summary>
public readonly record struct CardProgressId(Guid Value)
{
    public static CardProgressId New() => new(IdGenerator.NewId());
}
