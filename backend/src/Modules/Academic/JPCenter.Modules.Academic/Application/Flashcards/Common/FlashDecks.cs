using JPCenter.Modules.Academic.Application.Abstractions;
using JPCenter.Modules.Academic.Application.Courses.Common;
using JPCenter.Modules.Academic.Domain.Flashcards;
using Microsoft.EntityFrameworkCore;

namespace JPCenter.Modules.Academic.Application.Flashcards.Common;

/// <summary>Đọc / nạp bộ thẻ (thẻ là bảng con, luôn nạp kèm) — sắp thẻ theo thứ tự trước khi sửa để đánh số lại đúng</summary>
public sealed class FlashDecks(IAcademicDbContext db)
{
    public async Task<FlashDeck?> ForUpdateAsync(Guid id, CancellationToken ct)
    {
        var deck = await db.FlashDecks.FirstOrDefaultAsync(d => d.Id == new FlashDeckId(id), ct);
        deck?.Reorder(deck.Cards.OrderBy(c => c.Order).Select(c => c.Id).ToList());
        return deck;
    }

    public static FlashDeckDto ToDto(FlashDeck d) => new(d.Id.Value, d.Title, LevelText.Of(d.Level), d.Kind, d.LessonId?.Value, d.LessonLabel,
        d.Cards.OrderBy(c => c.Order).Select(c => new FlashCardDto(c.Id.Value, c.Front, c.Reading, c.Meaning, c.Example, c.Order)).ToList());
}
