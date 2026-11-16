using JPCenter.SharedKernel.Domain;
using JPCenter.SharedKernel.Primitives;
using JPCenter.Modules.Academic.Domain.Curricula;

namespace JPCenter.Modules.Academic.Domain.Flashcards;

/// <summary>Bộ flashcard từ vựng / Kanji theo bài học. Bảng: academic.flash_decks (+ flash_cards)</summary>
public sealed class FlashDeck : AggregateRoot<FlashDeckId>, IAuditable, ISoftDeletable
{
    private readonly List<FlashCard> _cards = [];

    private FlashDeck() { }

    public string Title { get; private set; } = null!;
    public JlptLevel Level { get; private set; }
    public DeckKind Kind { get; private set; }
    /// <summary>Bài học trong lộ trình</summary>
    public LessonId? LessonId { get; private set; }
    /// <summary>Nhãn bài học hiển thị, vd "Bài 5"</summary>
    public string LessonLabel { get; private set; } = null!;
    public IReadOnlyList<FlashCard> Cards => _cards;

    public static FlashDeck Create(string title, JlptLevel level, DeckKind kind, LessonId? lessonId, string lessonLabel) =>
        new() { Id = FlashDeckId.New(), Title = title, Level = level, Kind = kind, LessonId = lessonId, LessonLabel = lessonLabel };

    public FlashCard AddCard(string front, string? reading, string meaning, string? example)
    {
        var card = new FlashCard(front, reading, meaning, example, _cards.Count + 1);
        _cards.Add(card);
        return card;
    }

    public void RemoveCard(FlashCardId cardId) => _cards.RemoveAll(c => c.Id == cardId);
}
