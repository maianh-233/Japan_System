using JPCenter.SharedKernel.Domain;
using JPCenter.SharedKernel.Primitives;

namespace JPCenter.Modules.Academic.Domain.Flashcards;

/// <summary>1 thẻ: mặt trước (từ / Kanji), cách đọc, nghĩa, ví dụ. Bảng: academic.flash_cards</summary>
public sealed class FlashCard : Entity<FlashCardId>
{
    public const int MaxFront = 100;
    public const int MaxText = 500;

    private FlashCard() { }

    internal FlashCard(string front, string? reading, string meaning, string? example, int order) : base(FlashCardId.New()) =>
        (Front, Reading, Meaning, Example, Order) = (front, reading, meaning, example, order);

    public string Front { get; private set; } = null!;
    public string? Reading { get; private set; }
    public string Meaning { get; private set; } = null!;
    public string? Example { get; private set; }
    public int Order { get; private set; }

    internal static Result Validate(string? front, string? meaning)
    {
        if (string.IsNullOrWhiteSpace(front) || string.IsNullOrWhiteSpace(meaning)) return FlashcardErrors.CardRequired;
        if (front.Trim().Length > MaxFront || meaning.Trim().Length > MaxText) return FlashcardErrors.CardTooLong;
        return Result.Success();
    }

    internal Result Edit(string? front, string? reading, string? meaning, string? example)
    {
        var valid = Validate(front, meaning);
        if (valid.IsFailure) return valid;
        (Front, Reading, Meaning, Example) = (front!.Trim(), reading, meaning!.Trim(), example);
        return Result.Success();
    }

    internal void MoveTo(int order) => Order = order;
}
