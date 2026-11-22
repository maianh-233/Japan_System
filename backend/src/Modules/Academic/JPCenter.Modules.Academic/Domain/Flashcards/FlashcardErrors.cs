using JPCenter.SharedKernel.Primitives;

namespace JPCenter.Modules.Academic.Domain.Flashcards;

/// <summary>Lỗi flashcard (ND-09)</summary>
public static class FlashcardErrors
{
    public static readonly Error DeckNotFound = Error.NotFound("academic.flashcard.deck_not_found", "Không tìm thấy bộ thẻ");
    public static readonly Error CardNotFound = Error.NotFound("academic.flashcard.card_not_found", "Không tìm thấy thẻ");
    public static readonly Error TitleRequired = Error.Validation("academic.flashcard.title_required", "Nhập tên bộ thẻ");
    /// <summary>ND-09.E1</summary>
    public static readonly Error CardRequired = Error.Validation("academic.flashcard.card_required", "Thẻ cần mặt trước và nghĩa");
    public static readonly Error CardTooLong = Error.Validation("academic.flashcard.card_too_long", "Mặt trước tối đa 100, nghĩa tối đa 500 ký tự");
    public static readonly Error TooManyCards = Error.Validation("academic.flashcard.too_many_cards", "Mỗi bộ tối đa 500 thẻ");
    public static readonly Error InvalidOrder = Error.Validation("academic.flashcard.invalid_order", "Thứ tự thẻ không khớp với bộ thẻ");
    /// <summary>ND-09.E3</summary>
    public static readonly Error NotAvailable = Error.Forbidden("academic.flashcard.not_available", "Bộ thẻ không thuộc lộ trình lớp bạn đang học");
}
