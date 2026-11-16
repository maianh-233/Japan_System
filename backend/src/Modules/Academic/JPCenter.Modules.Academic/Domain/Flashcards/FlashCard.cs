using JPCenter.SharedKernel.Domain;

namespace JPCenter.Modules.Academic.Domain.Flashcards;

/// <summary>1 thẻ: mặt trước (từ / Kanji), cách đọc, nghĩa, ví dụ. Bảng: academic.flash_cards</summary>
public sealed class FlashCard : Entity<FlashCardId>
{
    private FlashCard() { }

    internal FlashCard(string front, string? reading, string meaning, string? example, int order) : base(FlashCardId.New()) =>
        (Front, Reading, Meaning, Example, Order) = (front, reading, meaning, example, order);

    public string Front { get; private set; } = null!;
    public string? Reading { get; private set; }
    public string Meaning { get; private set; } = null!;
    public string? Example { get; private set; }
    public int Order { get; private set; }
}
