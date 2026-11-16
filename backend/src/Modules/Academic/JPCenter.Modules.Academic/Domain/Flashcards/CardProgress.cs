using JPCenter.SharedKernel.Domain;
using JPCenter.SharedKernel.Primitives;

namespace JPCenter.Modules.Academic.Domain.Flashcards;

/// <summary>
/// Tiến độ ôn 1 thẻ của 1 HV — hộp Leitner 0–5: nhớ → lên hộp (giãn lịch), quên → về hộp 0.
/// Bảng: academic.card_progress, UQ (student_id, card_id)
/// </summary>
public sealed class CardProgress : AggregateRoot<CardProgressId>
{
    /// <summary>Số ngày chờ ôn lại theo hộp 0..5</summary>
    private static readonly int[] IntervalDays = [0, 1, 3, 7, 14, 30];

    private CardProgress() { }

    public Guid StudentId { get; private set; }
    public FlashDeckId DeckId { get; private set; }
    public FlashCardId CardId { get; private set; }
    public int Box { get; private set; }
    /// <summary>Ngày cần ôn tiếp</summary>
    public DateOnly Due { get; private set; }
    public int Reviews { get; private set; }
    public int Lapses { get; private set; }
    public DateTimeOffset? LastReviewedAt { get; private set; }

    public static CardProgress Start(Guid studentId, FlashDeckId deckId, FlashCardId cardId, DateOnly today) =>
        new() { Id = CardProgressId.New(), StudentId = studentId, DeckId = deckId, CardId = cardId, Box = 0, Due = today };

    public void Review(bool remembered, DateOnly today, DateTimeOffset at)
    {
        Reviews++;
        if (remembered) Box = Math.Min(Box + 1, IntervalDays.Length - 1);
        else { Box = 0; Lapses++; }
        Due = today.AddDays(IntervalDays[Box]);
        LastReviewedAt = at;
    }
}
