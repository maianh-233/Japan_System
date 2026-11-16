using JPCenter.SharedKernel.Primitives;

namespace JPCenter.Modules.Academic.Domain.Flashcards;

/// <summary>Id thẻ</summary>
public readonly record struct FlashCardId(Guid Value)
{
    public static FlashCardId New() => new(IdGenerator.NewId());
}
