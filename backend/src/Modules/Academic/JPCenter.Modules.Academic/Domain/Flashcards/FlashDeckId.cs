using JPCenter.SharedKernel.Primitives;

namespace JPCenter.Modules.Academic.Domain.Flashcards;

/// <summary>Id bộ flashcard</summary>
public readonly record struct FlashDeckId(Guid Value)
{
    public static FlashDeckId New() => new(IdGenerator.NewId());
}
