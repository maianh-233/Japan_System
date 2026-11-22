using JPCenter.SharedKernel.Domain;
using JPCenter.SharedKernel.Primitives;
using JPCenter.Modules.Academic.Domain.Curricula;

namespace JPCenter.Modules.Academic.Domain.Flashcards;

/// <summary>
/// Bộ flashcard từ vựng / Kanji theo bài học của lộ trình (ND-09). Đào tạo soạn (<c>academic.curriculum.update</c>); HV ôn các bộ của bài
/// trong lộ trình lớp đang học. Bảng: academic.flash_decks (+ flash_cards)
/// </summary>
public sealed class FlashDeck : AggregateRoot<FlashDeckId>, IAuditable, ISoftDeletable
{
    public const int MaxCards = 500;

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

    public static Result<FlashDeck> Create(string? title, JlptLevel level, DeckKind kind, LessonId? lessonId, string? lessonLabel)
    {
        var deck = new FlashDeck { Id = FlashDeckId.New() };
        var set = deck.Update(title, level, kind, lessonId, lessonLabel);
        return set.IsFailure ? set.Error : deck;
    }

    public Result Update(string? title, JlptLevel level, DeckKind kind, LessonId? lessonId, string? lessonLabel)
    {
        if (string.IsNullOrWhiteSpace(title)) return FlashcardErrors.TitleRequired;
        (Title, Level, Kind, LessonId, LessonLabel) = (title.Trim(), level, kind, lessonId, lessonLabel?.Trim() ?? "");
        return Result.Success();
    }

    /// <summary>Thêm thẻ cuối bộ — mặt trước + nghĩa bắt buộc (E1)</summary>
    public Result<FlashCard> AddCard(string? front, string? reading, string? meaning, string? example)
    {
        if (_cards.Count >= MaxCards) return FlashcardErrors.TooManyCards;
        var valid = FlashCard.Validate(front, meaning);
        if (valid.IsFailure) return valid.Error;
        var card = new FlashCard(front!.Trim(), Clean(reading), meaning!.Trim(), Clean(example), _cards.Count + 1);
        _cards.Add(card);
        return card;
    }

    public Result UpdateCard(FlashCardId cardId, string? front, string? reading, string? meaning, string? example)
    {
        if (_cards.FirstOrDefault(c => c.Id == cardId) is not { } card) return FlashcardErrors.CardNotFound;
        return card.Edit(front, Clean(reading), meaning, Clean(example));
    }

    /// <summary>E2 — xóa thẻ (tiến độ ôn của thẻ do Application xóa theo)</summary>
    public Result RemoveCard(FlashCardId cardId)
    {
        if (_cards.RemoveAll(c => c.Id == cardId) == 0) return FlashcardErrors.CardNotFound;
        Renumber(_cards);
        return Result.Success();
    }

    /// <summary>Sắp xếp lại theo danh sách Id (đủ mọi thẻ của bộ)</summary>
    public Result Reorder(IReadOnlyList<FlashCardId> order)
    {
        if (order.Count != _cards.Count || order.Distinct().Count() != order.Count || order.Any(id => _cards.All(c => c.Id != id)))
            return FlashcardErrors.InvalidOrder;
        Renumber(order.Select(id => _cards.First(c => c.Id == id)).ToList());
        _cards.Sort((a, b) => a.Order.CompareTo(b.Order));
        return Result.Success();
    }

    private static void Renumber(List<FlashCard> cards)
    {
        for (var i = 0; i < cards.Count; i++) cards[i].MoveTo(i + 1);
    }

    private static string? Clean(string? s) => string.IsNullOrWhiteSpace(s) ? null : s.Trim();
}
