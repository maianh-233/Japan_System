using JPCenter.Application.Common.Messaging;
using JPCenter.Modules.Academic.Application.Abstractions;
using JPCenter.Modules.Academic.Application.Flashcards.Common;
using JPCenter.Modules.Academic.Domain.Curricula;
using JPCenter.Modules.Academic.Domain.Flashcards;
using JPCenter.SharedKernel.Primitives;
using Microsoft.EntityFrameworkCore;

namespace JPCenter.Modules.Academic.Application.Flashcards.GetFlashDecks;

/// <summary>ND-09 — danh sách bộ thẻ (Lộ trình › bài học › Flashcard): lọc theo lộ trình (mọi bài) / bài học / trình độ</summary>
public sealed record GetFlashDecksQuery(Guid? CurriculumId, Guid? LessonId, JlptLevel? Level) : IQuery<IReadOnlyList<FlashDeckDto>>;

/// <summary>1 bộ thẻ</summary>
public sealed record GetFlashDeckQuery(Guid Id) : IQuery<FlashDeckDto>;

public sealed class GetFlashDecksHandler(IAcademicDbContext db) : IQueryHandler<GetFlashDecksQuery, IReadOnlyList<FlashDeckDto>>,
    IQueryHandler<GetFlashDeckQuery, FlashDeckDto>
{
    public async Task<Result<IReadOnlyList<FlashDeckDto>>> Handle(GetFlashDecksQuery q, CancellationToken ct)
    {
        var query = db.FlashDecks.AsNoTracking();
        if (q.LessonId is { } l) query = query.Where(d => d.LessonId == new LessonId(l));
        if (q.Level is { } level) query = query.Where(d => d.Level == level);
        if (q.CurriculumId is { } cid)
        {
            var c = await db.Curricula.AsNoTracking().AsSplitQuery().FirstOrDefaultAsync(x => x.Id == new CurriculumId(cid), ct);
            var lessons = c?.Lessons.Select(x => (LessonId?)x.Id).ToList() ?? [];
            query = query.Where(d => lessons.Contains(d.LessonId));
        }
        var list = await query.OrderBy(d => d.Title).ToListAsync(ct);
        return list.Select(FlashDecks.ToDto).ToList();
    }

    public async Task<Result<FlashDeckDto>> Handle(GetFlashDeckQuery q, CancellationToken ct) =>
        await db.FlashDecks.AsNoTracking().FirstOrDefaultAsync(d => d.Id == new FlashDeckId(q.Id), ct) is { } d
            ? FlashDecks.ToDto(d)
            : FlashcardErrors.DeckNotFound;
}
